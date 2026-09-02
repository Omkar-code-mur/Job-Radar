using JobRadar.Api.Auth;
using JobRadar.Api.Sources;
using JobRadar.Api.Sources.Greenhouse;
using JobRadar.Api.Sources.Deloitte;
using Microsoft.AspNetCore.Authentication.JwtBearer;
using Microsoft.IdentityModel.Tokens;
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
var adminEmail = builder.Configuration["JOBRADAR_ADMIN_EMAIL"]
    ?? Environment.GetEnvironmentVariable("JOBRADAR_ADMIN_EMAIL");

if (string.IsNullOrWhiteSpace(supabaseUrl))
    throw new InvalidOperationException("SUPABASE_URL must be configured.");

var supabaseIssuer = $"{supabaseUrl.TrimEnd('/')}/auth/v1";
builder.Services.AddAuthentication(JwtBearerDefaults.AuthenticationScheme)
    .AddJwtBearer(options =>
    {
        options.Authority = supabaseIssuer;
        options.MetadataAddress = $"{supabaseIssuer}/.well-known/openid-configuration";
        options.TokenValidationParameters = new TokenValidationParameters
        {
            ValidateIssuer = true,
            ValidIssuer = supabaseIssuer,
            ValidateAudience = true,
            ValidAudience = "authenticated",
            ValidateLifetime = true,
            ValidateIssuerSigningKey = true,
            ClockSkew = TimeSpan.FromSeconds(30),
        };
    });
builder.Services.AddAuthorization();

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
builder.Services.AddScoped<IJobSourceFetcher, GreenhouseJobSource>();
builder.Services.AddScoped<IJobSourceFetcher, DeloitteUsiJobSource>();
builder.Services.AddScoped<JobSourceFetcherFactory>();

var connectionString = builder.Configuration["ConnectionStrings:DefaultConnection"]
    ?? builder.Configuration["DATABASE_URL"]
    ?? Environment.GetEnvironmentVariable("DATABASE_URL");
if (string.IsNullOrWhiteSpace(connectionString))
    throw new InvalidOperationException("DATABASE_URL must be configured.");

var store = new PostgresJobRadarStore(connectionString);
var userIdentityStore = new UserIdentityStore(connectionString);
await store.InitializeAsync();
await userIdentityStore.InitializeAsync();

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

api.MapGet("/auth/me", async (HttpContext context, CancellationToken ct) =>
{
    var user = await userIdentityStore.GetOrCreateAsync(context.User, adminEmail, ct);
    return user is null
        ? Results.Unauthorized()
        : Results.Ok(user);
});

api.MapGet("/dashboard", async (CancellationToken ct) => Results.Ok(await store.DashboardAsync(ct)));
api.MapGet("/companies", async (CancellationToken ct) => Results.Ok(await store.GetCompaniesAsync(ct)));
api.MapPost("/companies", async (CompanyInput input, CancellationToken ct) =>
{
    if (string.IsNullOrWhiteSpace(input.Name) || string.IsNullOrWhiteSpace(input.Domain))
        return Results.BadRequest(new { error = "Company name and domain are required." });
    var company = await store.AddCompanyAsync(input, ct);
    return Results.Created($"/api/companies/{company.Id}", company);
});
api.MapPatch("/companies/{id}", async (string id, CompanyUpdate input, CancellationToken ct) =>
{
    var company = await store.UpdateCompanyAsync(id, input, ct);
    return company is null ? Results.NotFound(new { error = "Company not found." }) : Results.Ok(company);
});
api.MapDelete("/companies/{id}", async (string id, CancellationToken ct) =>
    await store.DeleteCompanyAsync(id, ct) ? Results.NoContent() : Results.NotFound(new { error = "Company not found." }));

api.MapGet("/sources", async (CancellationToken ct) => Results.Ok(await store.GetSourcesAsync(ct)));
api.MapPost("/sources", async (SourceInput input, CancellationToken ct) =>
{
    var source = await store.AddSourceAsync(input, ct);
    return source is null
        ? Results.BadRequest(new { error = "Company, source name, type, and URL are required." })
        : Results.Created($"/api/sources/{source.Id}", source);
});
api.MapPatch("/sources/{id}", async (string id, SourceUpdate input, CancellationToken ct) =>
{
    var source = await store.UpdateSourceAsync(id, input, ct);
    return source is null ? Results.NotFound(new { error = "Source not found." }) : Results.Ok(source);
});
api.MapDelete("/sources/{id}", async (string id, CancellationToken ct) =>
    await store.DeleteSourceAsync(id, ct) ? Results.NoContent() : Results.NotFound(new { error = "Source not found." }));
api.MapPost("/sources/{id}/scan",
    async (
        string id,
        JobSourceFetcherFactory sourceFetcherFactory,
        CancellationToken ct) =>
        Results.Ok(await store.ScanAsync(
            [id],
            sourceFetcherFactory,
            ct)));
api.MapGet("/jobs", async (string? search, string? status, string? location, string? workplaceType, CancellationToken ct) =>
    Results.Ok(await store.GetJobsAsync(search, status, location, workplaceType, ct)));
api.MapGet("/jobs/{id}", async (string id, CancellationToken ct) =>
{
    var job = await store.GetJobAsync(id, ct);
    return job is null ? Results.NotFound(new { error = "Job not found." }) : Results.Ok(job);
});
api.MapGet("/profile", async (CancellationToken ct) => Results.Ok(await store.GetProfileAsync(ct)));
api.MapPut("/profile", async (ProfileInput input, CancellationToken ct) => Results.Ok(await store.SaveProfileAsync(input, ct)));
api.MapGet("/matching", async (CancellationToken ct) => Results.Ok(await store.GetMatchingAsync(ct)));
api.MapPut("/matching", async (MatchingConfiguration input, CancellationToken ct) =>
{
    var total = input.RoleWeight + input.SkillsWeight + input.ExperienceWeight + input.LocationWeight + input.AiWeight + input.FreshnessWeight;
    if (input.Threshold is < 0 or > 100 || total != 100)
        return Results.BadRequest(new { error = "Scoring weights must total 100." });
    await store.SaveMatchingAsync(input, ct);
    return Results.Ok(input);
});
api.MapGet("/notifications", async (CancellationToken ct) => Results.Ok(await store.GetNotificationsAsync(ct)));
api.MapPost("/scheduler/scan",
    async (
        JobSourceFetcherFactory sourceFetcherFactory,
        CancellationToken ct) =>
        Results.Ok(await store.ScanAsync(
            (await store.GetSourcesAsync(ct))
                .Select(source => source.Id)
                .ToArray(),
            sourceFetcherFactory,
            ct)));

app.Run();

public record Company(string Id, string Name, string Domain, string Initials, string Color, bool Enabled, int SourceCount, int JobCount, string CreatedAt);
public record CompanyInput(string Name, string Domain);
public record CompanyUpdate(string? Name, string? Domain, bool? Enabled);
public record JobSource(string Id, string CompanyId, string CompanyName, string Name, string Type, string Url, bool Enabled, string Status, string LastFetch, int JobsFetched, int FailureCount, string? LastError, string? BoardToken);
public record SourceInput(string CompanyId, string Name, string Type, string Url, string? BoardToken = null);
public record SourceUpdate(
    string? Name,
    string? Url,
    bool? Enabled,
    string? BoardToken);public record Job(string Id, string CompanyId, string SourceId, string Company, string Title, string Description, string Location, string WorkplaceType, string Department, string EmploymentType, string PostedDate, string FirstSeenAt, string ApplicationUrl, string SourceUrl, int Score, bool IsMatch, bool Notified, string[] MatchedSkills, string[] MissingSkills, Breakdown Breakdown);
public record Breakdown(int Role, int Skills, int Experience, int Location, int AiRelevance, int Freshness);
public record Profile(string Id, string[] Roles, string[] Skills, string[] Technologies, int MinYears, int MaxYears, string[] Locations, string WorkplacePreference, string[] IncludeKeywords, string[] ExcludeKeywords, string Email);
public record ProfileInput(string[] Roles, string[] Skills, string[] Technologies, int MinYears, int MaxYears, string[] Locations, string WorkplacePreference, string[] IncludeKeywords, string[] ExcludeKeywords, string Email);
public record MatchingConfiguration(int Threshold, int RoleWeight, int SkillsWeight, int ExperienceWeight, int LocationWeight, int AiWeight, int FreshnessWeight);
public record Notification(string Id, string JobId, string JobTitle, string Company, int Score, string Type, string SentAt, string Status, string? Error);
public record ScanResult(int SourcesScanned, int JobsFetched, int NewJobs, int MatchedJobs, int NotificationsSent);
