using JobRadar.Api.Auth;
using JobRadar.Api.Sources;
using JobRadar.Api.Sources.Greenhouse;
using JobRadar.Api.Sources.Deloitte;
using JobRadar.Api.Sources.Workday;
using Microsoft.AspNetCore.Authentication;
using System.Diagnostics;

var builder = WebApplication.CreateBuilder(args);
builder.Logging.AddSimpleConsole(options =>
{
    options.TimestampFormat = "yyyy-MM-dd HH:mm:ss ";
    options.SingleLine = true;
});
builder.Services.ConfigureHttpJsonOptions(options =>
    options.SerializerOptions.PropertyNamingPolicy = System.Text.Json.JsonNamingPolicy.CamelCase);
builder.Services.AddCors(options => options.AddDefaultPolicy(policy =>
    policy.AllowAnyOrigin().AllowAnyHeader().AllowAnyMethod()));

var supabaseUrl = builder.Configuration["SUPABASE_URL"]
    ?? Environment.GetEnvironmentVariable("SUPABASE_URL");
var supabaseKey = builder.Configuration["SUPABASE_ANON_KEY"]
    ?? builder.Configuration["SUPABASE_PUBLISHABLE_KEY"]
    ?? Environment.GetEnvironmentVariable("SUPABASE_ANON_KEY")
    ?? Environment.GetEnvironmentVariable("SUPABASE_PUBLISHABLE_KEY");
var adminEmail = builder.Configuration["JOBRADAR_ADMIN_EMAIL"]
    ?? Environment.GetEnvironmentVariable("JOBRADAR_ADMIN_EMAIL");

if (string.IsNullOrWhiteSpace(supabaseUrl))
    throw new InvalidOperationException("SUPABASE_URL must be configured.");
if (string.IsNullOrWhiteSpace(supabaseKey))
    throw new InvalidOperationException("SUPABASE_ANON_KEY or SUPABASE_PUBLISHABLE_KEY must be configured.");

builder.Services.AddAuthentication("Supabase")
    .AddScheme<AuthenticationSchemeOptions, SupabaseAuthenticationHandler>("Supabase", _ => { });
builder.Services.AddAuthorization();

builder.Services.AddHttpClient("SupabaseAuth", client =>
{
    client.Timeout = TimeSpan.FromSeconds(10);
});
builder.Services.AddHttpClient<GreenhouseJobSource>(client =>
{
    client.Timeout = TimeSpan.FromSeconds(10);
    client.DefaultRequestHeaders.UserAgent.ParseAdd("JobRadar/1.0 public-job-monitor");
});
builder.Services.AddHttpClient<DeloitteUsiJobSource>(client =>
{
    client.Timeout = TimeSpan.FromSeconds(20);
    client.DefaultRequestHeaders.UserAgent.ParseAdd("Mozilla/5.0 (compatible; JobRadar/1.0; public-job-monitor)");
});
builder.Services.AddHttpClient<WorkdayJobSource>(client =>
{
    client.Timeout = TimeSpan.FromSeconds(20);
    client.DefaultRequestHeaders.UserAgent.ParseAdd("JobRadar/1.0 public-job-monitor");
});
builder.Services.AddScoped<IJobSourceFetcher, GreenhouseJobSource>();
builder.Services.AddScoped<IJobSourceFetcher, DeloitteUsiJobSource>();
builder.Services.AddScoped<IJobSourceFetcher, WorkdayJobSource>();
builder.Services.AddScoped<JobSourceFetcherFactory>();

var connectionString = builder.Configuration["ConnectionStrings:DefaultConnection"]
    ?? builder.Configuration["DATABASE_URL"]
    ?? Environment.GetEnvironmentVariable("DATABASE_URL");
if (string.IsNullOrWhiteSpace(connectionString))
    throw new InvalidOperationException("DATABASE_URL must be configured.");

var baseStore = new PostgresJobRadarStore(connectionString);
var store = new UserScopedJobRadarStore(baseStore, connectionString);
var workspaceStore = new UserWorkspaceStore(connectionString);
var userIdentityStore = new UserIdentityStore(connectionString);
await userIdentityStore.InitializeAsync();
await store.InitializeAsync();
await workspaceStore.InitializeAsync();

var app = builder.Build();
app.UseCors();
app.UseAuthentication();
app.UseAuthorization();

app.Use(async (context, next) =>
{
    var requestId = context.Request.Headers["X-Request-ID"].FirstOrDefault()
        ?? context.TraceIdentifier;
    context.Response.Headers["X-Request-ID"] = requestId;

    using (app.Logger.BeginScope(new Dictionary<string, object>
    {
        ["RequestId"] = requestId,
        ["Method"] = context.Request.Method,
        ["Path"] = context.Request.Path.Value ?? string.Empty,
    }))
    {
        var stopwatch = Stopwatch.StartNew();
        app.Logger.LogInformation("API request started {Method} {Path}", context.Request.Method, context.Request.Path);
        try
        {
            await next(context);
            app.Logger.LogInformation(
                "API request completed {Method} {Path} with {StatusCode} in {ElapsedMs} ms",
                context.Request.Method,
                context.Request.Path,
                context.Response.StatusCode,
                stopwatch.ElapsedMilliseconds);
        }
        catch (OperationCanceledException) when (context.RequestAborted.IsCancellationRequested)
        {
            app.Logger.LogWarning(
                "API request cancelled {Method} {Path} after {ElapsedMs} ms",
                context.Request.Method,
                context.Request.Path,
                stopwatch.ElapsedMilliseconds);
            throw;
        }
        catch (Exception exception)
        {
            app.Logger.LogError(
                exception,
                "API request failed {Method} {Path} after {ElapsedMs} ms",
                context.Request.Method,
                context.Request.Path,
                stopwatch.ElapsedMilliseconds);
            throw;
        }
    }
});

app.MapGet("/api/healthz", () => Results.Ok(new { status = "ok" }));

var api = app.MapGroup("/api").RequireAuthorization();
var monitorableSourceTypes = new HashSet<string>(StringComparer.OrdinalIgnoreCase)
{
    "GREENHOUSE_API",
    "DELOITTE_USI",
    "WORKDAY_API"
};

bool IsMonitorableSource(JobSource source) =>
    source.Enabled &&
    source.Status == "healthy" &&
    monitorableSourceTypes.Contains(source.Type);

api.MapGet("/auth/me", async (HttpContext context, CancellationToken ct) =>
{
    var user = await userIdentityStore.GetOrCreateAsync(context.User, adminEmail, ct);
    return user is null
        ? Results.Unauthorized()
        : Results.Ok(user);
});

api.MapGet("/dashboard", async (HttpContext context, CancellationToken ct) =>
{
    var user = await userIdentityStore.GetOrCreateAsync(context.User, adminEmail, ct);
    return user is null ? Results.Unauthorized() : Results.Ok(await store.DashboardAsync(user.Id, ct));
});

api.MapGet("/companies", async (HttpContext context, CancellationToken ct) =>
{
    var user = await userIdentityStore.GetOrCreateAsync(context.User, adminEmail, ct);
    if (user is null) return Results.Unauthorized();

    var companies = await store.GetCompaniesAsync(ct);
    if (user.Role == "ADMIN") return Results.Ok(companies);

    var sources = await store.GetSourcesAsync(ct);
    var monitorableCompanyIds = sources
        .Where(IsMonitorableSource)
        .Select(source => source.CompanyId)
        .ToHashSet(StringComparer.Ordinal);

    return Results.Ok(companies.Where(company => monitorableCompanyIds.Contains(company.Id)));
});
api.MapPost("/companies", async (HttpContext context, CompanyInput input, CancellationToken ct) =>
{
    var user = await userIdentityStore.GetOrCreateAsync(context.User, adminEmail, ct);
    if (user?.Role != "ADMIN") return Results.Forbid();
    if (string.IsNullOrWhiteSpace(input.Name) || string.IsNullOrWhiteSpace(input.Domain))
        return Results.BadRequest(new { error = "Company name and domain are required." });
    var company = await store.AddCompanyAsync(input, ct);
    return Results.Created($"/api/companies/{company.Id}", company);
});
api.MapPatch("/companies/{id}", async (HttpContext context, string id, CompanyUpdate input, CancellationToken ct) =>
{
    var user = await userIdentityStore.GetOrCreateAsync(context.User, adminEmail, ct);
    if (user?.Role != "ADMIN") return Results.Forbid();
    var company = await store.UpdateCompanyAsync(id, input, ct);
    return company is null ? Results.NotFound(new { error = "Company not found." }) : Results.Ok(company);
});
api.MapDelete("/companies/{id}", async (HttpContext context, string id, CancellationToken ct) =>
{
    var user = await userIdentityStore.GetOrCreateAsync(context.User, adminEmail, ct);
    if (user?.Role != "ADMIN") return Results.Forbid();
    return await store.DeleteCompanyAsync(id, ct) ? Results.NoContent() : Results.NotFound(new { error = "Company not found." });
});

api.MapGet("/sources", async (CancellationToken ct) => Results.Ok(await store.GetSourcesAsync(ct)));
api.MapPost("/sources", async (HttpContext context, SourceInput input, CancellationToken ct) =>
{
    var user = await userIdentityStore.GetOrCreateAsync(context.User, adminEmail, ct);
    if (user?.Role != "ADMIN") return Results.Forbid();
    var source = await store.AddSourceAsync(input, ct);
    return source is null
        ? Results.BadRequest(new { error = "Company, source name, type, and URL are required." })
        : Results.Created($"/api/sources/{source.Id}", source);
});
api.MapPatch("/sources/{id}", async (HttpContext context, string id, SourceUpdate input, CancellationToken ct) =>
{
    var user = await userIdentityStore.GetOrCreateAsync(context.User, adminEmail, ct);
    if (user?.Role != "ADMIN") return Results.Forbid();
    var source = await store.UpdateSourceAsync(id, input, ct);
    return source is null ? Results.NotFound(new { error = "Source not found." }) : Results.Ok(source);
});
api.MapDelete("/sources/{id}", async (HttpContext context, string id, CancellationToken ct) =>
{
    var user = await userIdentityStore.GetOrCreateAsync(context.User, adminEmail, ct);
    if (user?.Role != "ADMIN") return Results.Forbid();
    return await store.DeleteSourceAsync(id, ct) ? Results.NoContent() : Results.NotFound(new { error = "Source not found." });
});
api.MapPost("/sources/{id}/scan",
    async (
        string id,
        HttpContext context,
        JobSourceFetcherFactory sourceFetcherFactory,
        CancellationToken ct) =>
    {
        var user = await userIdentityStore.GetOrCreateAsync(context.User, adminEmail, ct);
        if (user?.Role != "ADMIN") return Results.Forbid();
        return Results.Ok(await store.ScanAsync(user.Id, [id], sourceFetcherFactory, ct));
    });
api.MapGet("/jobs", async (HttpContext context, string? search, string? status, string? location, string? workplaceType, CancellationToken ct) =>
{
    var user = await userIdentityStore.GetOrCreateAsync(context.User, adminEmail, ct);
    return user is null
        ? Results.Unauthorized()
        : Results.Ok(await store.GetJobsAsync(user.Id, search, status, location, workplaceType, ct));
});
api.MapGet("/jobs/{id}", async (HttpContext context, string id, CancellationToken ct) =>
{
    var user = await userIdentityStore.GetOrCreateAsync(context.User, adminEmail, ct);
    if (user is null) return Results.Unauthorized();
    var job = await store.GetJobAsync(user.Id, id, ct);
    return job is null ? Results.NotFound(new { error = "Job not found." }) : Results.Ok(job);
});
api.MapGet("/profile", async (HttpContext context, CancellationToken ct) =>
{
    var user = await userIdentityStore.GetOrCreateAsync(context.User, adminEmail, ct);
    return user is null ? Results.Unauthorized() : Results.Ok(await store.GetProfileAsync(user.Id, ct));
});
api.MapPut("/profile", async (HttpContext context, ProfileInput input, CancellationToken ct) =>
{
    var user = await userIdentityStore.GetOrCreateAsync(context.User, adminEmail, ct);
    return user is null ? Results.Unauthorized() : Results.Ok(await store.SaveProfileAsync(user.Id, input, ct));
});
api.MapGet("/matching", async (HttpContext context, CancellationToken ct) =>
{
    var user = await userIdentityStore.GetOrCreateAsync(context.User, adminEmail, ct);
    return user is null ? Results.Unauthorized() : Results.Ok(await store.GetMatchingAsync(user.Id, ct));
});
api.MapPut("/matching", async (HttpContext context, MatchingConfiguration input, CancellationToken ct) =>
{
    var total = input.RoleWeight + input.SkillsWeight + input.ExperienceWeight + input.LocationWeight + input.AiWeight + input.FreshnessWeight;
    if (input.Threshold is < 0 or > 100 || total != 100)
        return Results.BadRequest(new { error = "Scoring weights must total 100." });
    var user = await userIdentityStore.GetOrCreateAsync(context.User, adminEmail, ct);
    if (user is null) return Results.Unauthorized();
    await store.SaveMatchingAsync(user.Id, input, ct);
    return Results.Ok(input);
});
api.MapGet("/notifications", async (CancellationToken ct) => Results.Ok(await store.GetNotificationsAsync(ct)));

api.MapGet("/applications", async (HttpContext context, CancellationToken ct) =>
{
    var user = await userIdentityStore.GetOrCreateAsync(context.User, adminEmail, ct);
    return user is null ? Results.Unauthorized() : Results.Ok(await workspaceStore.GetApplicationsAsync(user.Id, ct));
});
api.MapPut("/applications/{jobId}", async (HttpContext context, string jobId, ApplicationInput input, CancellationToken ct) =>
{
    var user = await userIdentityStore.GetOrCreateAsync(context.User, adminEmail, ct);
    if (user is null) return Results.Unauthorized();
    try
    {
        var application = await workspaceStore.UpsertApplicationAsync(user.Id, jobId, input, ct);
        return application is null ? Results.NotFound(new { error = "Job not found." }) : Results.Ok(application);
    }
    catch (ArgumentException exception)
    {
        return Results.BadRequest(new { error = exception.Message });
    }
});

api.MapGet("/dream-companies", async (HttpContext context, CancellationToken ct) =>
{
    var user = await userIdentityStore.GetOrCreateAsync(context.User, adminEmail, ct);
    if (user is null) return Results.Unauthorized();

    var dreamCompanies = await workspaceStore.GetDreamCompaniesAsync(user.Id, ct);
    if (user.Role == "ADMIN") return Results.Ok(dreamCompanies);

    var sources = await store.GetSourcesAsync(ct);
    var monitorableCompanyIds = sources
        .Where(IsMonitorableSource)
        .Select(source => source.CompanyId)
        .ToHashSet(StringComparer.Ordinal);

    return Results.Ok(dreamCompanies.Where(company => monitorableCompanyIds.Contains(company.Id)));
});
api.MapPost("/dream-companies/{companyId}", async (HttpContext context, string companyId, CancellationToken ct) =>
{
    var user = await userIdentityStore.GetOrCreateAsync(context.User, adminEmail, ct);
    if (user is null) return Results.Unauthorized();

    var sources = await store.GetSourcesAsync(ct);
    if (!sources.Any(source => source.CompanyId == companyId && IsMonitorableSource(source)))
        return Results.BadRequest(new { error = "This company is not currently monitorable by Job Radar." });

    await workspaceStore.AddDreamCompanyAsync(user.Id, companyId, ct);
    return Results.NoContent();
});
api.MapDelete("/dream-companies/{companyId}", async (HttpContext context, string companyId, CancellationToken ct) =>
{
    var user = await userIdentityStore.GetOrCreateAsync(context.User, adminEmail, ct);
    if (user is null) return Results.Unauthorized();
    await workspaceStore.RemoveDreamCompanyAsync(user.Id, companyId, ct);
    return Results.NoContent();
});

api.MapGet("/workspace/settings", async (HttpContext context, CancellationToken ct) =>
{
    var user = await userIdentityStore.GetOrCreateAsync(context.User, adminEmail, ct);
    if (user is null) return Results.Unauthorized();
    return Results.Ok(await workspaceStore.GetWorkspaceSettingsAsync(ct));
});
api.MapPut("/workspace/settings", async (HttpContext context, WorkspaceSettings input, CancellationToken ct) =>
{
    var user = await userIdentityStore.GetOrCreateAsync(context.User, adminEmail, ct);
    if (user?.Role != "ADMIN") return Results.Forbid();
    await workspaceStore.SaveWorkspaceSettingsAsync(input, ct);
    return Results.Ok(input);
});

api.MapPost("/scheduler/scan",
    async (
        HttpContext context,
        JobSourceFetcherFactory sourceFetcherFactory,
        CancellationToken ct) =>
    {
        var user = await userIdentityStore.GetOrCreateAsync(context.User, adminEmail, ct);
        if (user?.Role != "ADMIN") return Results.Forbid();
        return Results.Ok(await store.ScanAsync(
            user.Id,
            (await store.GetSourcesAsync(ct))
                .Select(source => source.Id)
                .ToArray(),
            sourceFetcherFactory,
            ct));
    });

app.Run();

public record Company(string Id, string Name, string Domain, string Initials, string Color, bool Enabled, int SourceCount, int JobCount, string CreatedAt);
public record CompanyInput(string Name, string Domain);
public record CompanyUpdate(string? Name, string? Domain, bool? Enabled);
public record JobSource(string Id, string CompanyId, string CompanyName, string Name, string Type, string Url, bool Enabled, string Status, string LastFetch, int JobsFetched, int FailureCount, string? LastError, string? BoardToken);
public record SourceInput(string CompanyId, string Name, string Type, string Url, string? BoardToken = null);
public record SourceUpdate(string? Name, string? Url, bool? Enabled, string? BoardToken);
public record Job(string Id, string CompanyId, string SourceId, string Company, string Title, string Description, string Location, string WorkplaceType, string Department, string EmploymentType, string PostedDate, string FirstSeenAt, string ApplicationUrl, string SourceUrl, int Score, bool IsMatch, bool Notified, string[] MatchedSkills, string[] MissingSkills, Breakdown Breakdown);
public record Breakdown(int Role, int Skills, int Experience, int Location, int AiRelevance, int Freshness);
public record Profile(string Id, string[] Roles, string[] Skills, string[] Technologies, int MinYears, int MaxYears, string[] Locations, string WorkplacePreference, string[] IncludeKeywords, string[] ExcludeKeywords, string Email);
public record ProfileInput(string[] Roles, string[] Skills, string[] Technologies, int MinYears, int MaxYears, string[] Locations, string WorkplacePreference, string[] IncludeKeywords, string[] ExcludeKeywords, string Email);
public record MatchingConfiguration(int Threshold, int RoleWeight, int SkillsWeight, int ExperienceWeight, int LocationWeight, int AiWeight, int FreshnessWeight);
public record Notification(string Id, string JobId, string JobTitle, string Company, int Score, string Type, string SentAt, string Status, string? Error);
public record ScanResult(int SourcesScanned, int JobsFetched, int NewJobs, int MatchedJobs, int NotificationsSent);