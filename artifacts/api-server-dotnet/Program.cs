using JobRadar.Api.Sources.Greenhouse;

var builder = WebApplication.CreateBuilder(args);
builder.Services.ConfigureHttpJsonOptions(options =>
    options.SerializerOptions.PropertyNamingPolicy = System.Text.Json.JsonNamingPolicy.CamelCase);
builder.Services.AddCors(options => options.AddDefaultPolicy(policy =>
    policy.AllowAnyOrigin().AllowAnyHeader().AllowAnyMethod()));
builder.Services.AddHttpClient<GreenhouseJobSource>(client =>
{
    client.Timeout = TimeSpan.FromSeconds(10);
    client.DefaultRequestHeaders.UserAgent.ParseAdd("JobRadar/1.0 public-job-monitor");
});

var connectionString = builder.Configuration["ConnectionStrings:DefaultConnection"]
    ?? builder.Configuration["DATABASE_URL"]
    ?? Environment.GetEnvironmentVariable("DATABASE_URL");
var store = new PostgresJobRadarStore(connectionString ?? string.Empty);
await store.InitializeAsync();

var app = builder.Build();
app.UseCors();

app.MapGet("/api/healthz", () => Results.Ok(new { status = "ok" }));
app.MapGet("/api/dashboard", async (CancellationToken ct) => Results.Ok(await store.DashboardAsync(ct)));
app.MapGet("/api/companies", async (CancellationToken ct) => Results.Ok(await store.GetCompaniesAsync(ct)));
app.MapPost("/api/companies", async (CompanyInput input, CancellationToken ct) =>
{
    if (string.IsNullOrWhiteSpace(input.Name) || string.IsNullOrWhiteSpace(input.Domain))
        return Results.BadRequest(new { error = "Company name and domain are required." });
    var company = await store.AddCompanyAsync(input, ct);
    return Results.Created($"/api/companies/{company.Id}", company);
});
app.MapPatch("/api/companies/{id}", async (string id, CompanyUpdate input, CancellationToken ct) =>
{
    var company = await store.UpdateCompanyAsync(id, input, ct);
    return company is null ? Results.NotFound(new { error = "Company not found." }) : Results.Ok(company);
});
app.MapDelete("/api/companies/{id}", async (string id, CancellationToken ct) =>
    await store.DeleteCompanyAsync(id, ct) ? Results.NoContent() : Results.NotFound(new { error = "Company not found." }));

app.MapGet("/api/sources", async (CancellationToken ct) => Results.Ok(await store.GetSourcesAsync(ct)));
app.MapPost("/api/sources", async (SourceInput input, CancellationToken ct) =>
{
    var source = await store.AddSourceAsync(input, ct);
    return source is null
        ? Results.BadRequest(new { error = "Company, source name, type, and URL are required." })
        : Results.Created($"/api/sources/{source.Id}", source);
});
app.MapPatch("/api/sources/{id}", async (string id, SourceUpdate input, CancellationToken ct) =>
{
    var source = await store.UpdateSourceAsync(id, input, ct);
    return source is null ? Results.NotFound(new { error = "Source not found." }) : Results.Ok(source);
});
app.MapDelete("/api/sources/{id}", async (string id, CancellationToken ct) =>
    await store.DeleteSourceAsync(id, ct) ? Results.NoContent() : Results.NotFound(new { error = "Source not found." }));
app.MapPost("/api/sources/{id}/scan", async (string id, GreenhouseJobSource greenhouse, CancellationToken ct) =>
    Results.Ok(await store.ScanAsync([id], greenhouse, ct)));

app.MapGet("/api/jobs", async (string? search, string? status, string? location, string? workplaceType, CancellationToken ct) =>
    Results.Ok(await store.GetJobsAsync(search, status, location, workplaceType, ct)));
app.MapGet("/api/jobs/{id}", async (string id, CancellationToken ct) =>
{
    var job = await store.GetJobAsync(id, ct);
    return job is null ? Results.NotFound(new { error = "Job not found." }) : Results.Ok(job);
});
app.MapGet("/api/profile", async (CancellationToken ct) => Results.Ok(await store.GetProfileAsync(ct)));
app.MapPut("/api/profile", async (ProfileInput input, CancellationToken ct) => Results.Ok(await store.SaveProfileAsync(input, ct)));
app.MapGet("/api/matching", async (CancellationToken ct) => Results.Ok(await store.GetMatchingAsync(ct)));
app.MapPut("/api/matching", async (MatchingConfiguration input, CancellationToken ct) =>
{
    var total = input.RoleWeight + input.SkillsWeight + input.ExperienceWeight + input.LocationWeight + input.AiWeight + input.FreshnessWeight;
    if (input.Threshold is < 0 or > 100 || total != 100)
        return Results.BadRequest(new { error = "Scoring weights must total 100." });
    await store.SaveMatchingAsync(input, ct);
    return Results.Ok(input);
});
app.MapGet("/api/notifications", async (CancellationToken ct) => Results.Ok(await store.GetNotificationsAsync(ct)));
app.MapPost("/api/scheduler/scan", async (GreenhouseJobSource greenhouse, CancellationToken ct) =>
    Results.Ok(await store.ScanAsync((await store.GetSourcesAsync(ct)).Select(source => source.Id).ToArray(), greenhouse, ct)));

app.Run();

public record Company(string Id, string Name, string Domain, string Initials, string Color, bool Enabled, int SourceCount, int JobCount, string CreatedAt);
public record CompanyInput(string Name, string Domain);
public record CompanyUpdate(string? Name, string? Domain, bool? Enabled);
public record JobSource(string Id, string CompanyId, string CompanyName, string Name, string Type, string Url, bool Enabled, string Status, string LastFetch, int JobsFetched, int FailureCount, string? LastError, string? BoardToken);
public record SourceInput(string CompanyId, string Name, string Type, string Url, string? BoardToken = null);
public record SourceUpdate(string? Name, string? Url, bool? Enabled);
public record Job(string Id, string CompanyId, string SourceId, string Company, string Title, string Description, string Location, string WorkplaceType, string Department, string EmploymentType, string PostedDate, string FirstSeenAt, string ApplicationUrl, string SourceUrl, int Score, bool IsMatch, bool Notified, string[] MatchedSkills, string[] MissingSkills, Breakdown Breakdown);
public record Breakdown(int Role, int Skills, int Experience, int Location, int AiRelevance, int Freshness);
public record Profile(string Id, string[] Roles, string[] Skills, string[] Technologies, int MinYears, int MaxYears, string[] Locations, string WorkplacePreference, string[] IncludeKeywords, string[] ExcludeKeywords, string Email);
public record ProfileInput(string[] Roles, string[] Skills, string[] Technologies, int MinYears, int MaxYears, string[] Locations, string WorkplacePreference, string[] IncludeKeywords, string[] ExcludeKeywords, string Email);
public record MatchingConfiguration(int Threshold, int RoleWeight, int SkillsWeight, int ExperienceWeight, int LocationWeight, int AiWeight, int FreshnessWeight);
public record Notification(string Id, string JobId, string JobTitle, string Company, int Score, string Type, string SentAt, string Status, string? Error);
public record ScanResult(int SourcesScanned, int JobsFetched, int NewJobs, int MatchedJobs, int NotificationsSent);
