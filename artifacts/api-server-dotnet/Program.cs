using System.Collections.Concurrent;
using System.Text.Json.Serialization;

var builder = WebApplication.CreateBuilder(args);
builder.Services.ConfigureHttpJsonOptions(options =>
    options.SerializerOptions.PropertyNamingPolicy = System.Text.Json.JsonNamingPolicy.CamelCase);
builder.Services.AddCors(options => options.AddDefaultPolicy(policy =>
    policy.AllowAnyOrigin().AllowAnyHeader().AllowAnyMethod()));

var app = builder.Build();
app.UseCors();

var store = new JobRadarStore();

app.MapGet("/api/healthz", () => Results.Ok(new { status = "ok" }));
app.MapGet("/api/dashboard", () => Results.Ok(store.Dashboard()));
app.MapGet("/api/companies", () => Results.Ok(store.Companies));
app.MapPost("/api/companies", (CompanyInput input) =>
{
    if (string.IsNullOrWhiteSpace(input.Name) || string.IsNullOrWhiteSpace(input.Domain))
        return Results.BadRequest(new { error = "Company name and domain are required." });
    var company = store.AddCompany(input);
    return Results.Created($"/api/companies/{company.Id}", company);
});
app.MapPatch("/api/companies/{id}", (string id, CompanyUpdate input) =>
{
    var company = store.UpdateCompany(id, input);
    return company is null ? Results.NotFound(new { error = "Company not found." }) : Results.Ok(company);
});
app.MapDelete("/api/companies/{id}", (string id) =>
    store.DeleteCompany(id) ? Results.NoContent() : Results.NotFound(new { error = "Company not found." }));

app.MapGet("/api/sources", () => Results.Ok(store.Sources));
app.MapPost("/api/sources", (SourceInput input) =>
{
    var source = store.AddSource(input);
    return source is null
        ? Results.BadRequest(new { error = "Company, source name, type, and URL are required." })
        : Results.Created($"/api/sources/{source.Id}", source);
});
app.MapPatch("/api/sources/{id}", (string id, SourceUpdate input) =>
{
    var source = store.UpdateSource(id, input);
    return source is null ? Results.NotFound(new { error = "Source not found." }) : Results.Ok(source);
});
app.MapDelete("/api/sources/{id}", (string id) =>
    store.DeleteSource(id) ? Results.NoContent() : Results.NotFound(new { error = "Source not found." }));
app.MapPost("/api/sources/{id}/scan", (string id) => Results.Ok(store.Scan([id])));

app.MapGet("/api/jobs", (string? search, string? status, string? location, string? workplaceType) =>
    Results.Ok(store.FilterJobs(search, status, location, workplaceType)));
app.MapGet("/api/jobs/{id}", (string id) =>
{
    var job = store.Jobs.FirstOrDefault(item => item.Id == id);
    return job is null ? Results.NotFound(new { error = "Job not found." }) : Results.Ok(job);
});
app.MapGet("/api/profile", () => Results.Ok(store.Profile));
app.MapPut("/api/profile", (ProfileInput input) => { store.Profile = store.Profile with { Id = store.Profile.Id, Roles = input.Roles, Skills = input.Skills, Technologies = input.Technologies, MinYears = input.MinYears, MaxYears = input.MaxYears, Locations = input.Locations, WorkplacePreference = input.WorkplacePreference, IncludeKeywords = input.IncludeKeywords, ExcludeKeywords = input.ExcludeKeywords, Email = input.Email }; return Results.Ok(store.Profile); });
app.MapGet("/api/matching", () => Results.Ok(store.Matching));
app.MapPut("/api/matching", (MatchingConfiguration input) =>
{
    var total = input.RoleWeight + input.SkillsWeight + input.ExperienceWeight + input.LocationWeight + input.AiWeight + input.FreshnessWeight;
    if (input.Threshold is < 0 or > 100 || total != 100)
        return Results.BadRequest(new { error = "Scoring weights must total 100." });
    store.Matching = input;
    return Results.Ok(store.Matching);
});
app.MapGet("/api/notifications", () => Results.Ok(store.Notifications));
app.MapPost("/api/scheduler/scan", () => Results.Ok(store.Scan(store.Sources.Select(source => source.Id).ToArray())));

app.Run();

public sealed class JobRadarStore
{
    public List<Company> Companies { get; } =
    [
        new("company-1", "Microsoft", "microsoft.com", "MS", "#5B5CE2", true, 1, 2, DateTimeOffset.UtcNow.AddDays(-30).ToString("O")),
        new("company-2", "Atlassian", "atlassian.com", "AT", "#1D78D5", true, 1, 2, DateTimeOffset.UtcNow.AddDays(-28).ToString("O")),
        new("company-3", "Razorpay", "razorpay.com", "RZ", "#25A17A", true, 1, 1, DateTimeOffset.UtcNow.AddDays(-20).ToString("O"))
    ];
    public List<JobSource> Sources { get; } =
    [
        new("source-1", "company-1", "Microsoft", "Microsoft Careers", "GREENHOUSE_API", "https://careers.microsoft.com/search", true, "healthy", "1h ago", 2, 0, null),
        new("source-2", "company-2", "Atlassian", "Atlassian Jobs", "LEVER_API", "https://www.atlassian.com/company/careers", true, "healthy", "2h ago", 2, 0, null),
        new("source-3", "company-3", "Razorpay", "Razorpay Careers", "STRUCTURED_HTML", "https://razorpay.com/jobs", true, "warning", "25h ago", 1, 1, "Request timed out after 10 seconds on the previous attempt")
    ];
    public List<Job> Jobs { get; } =
    [
        new("job-1", "company-1", "source-1", "Microsoft", "Full Stack Software Engineer", "Build cloud services with React, TypeScript, Azure, and .NET. Work on AI-powered developer tools.", "Bangalore, India", "Hybrid", "Engineering", "Full-time", DateTimeOffset.UtcNow.AddHours(-9).ToString("O"), DateTimeOffset.UtcNow.AddHours(-8).ToString("O"), "https://careers.microsoft.com/job/1001", "https://careers.microsoft.com/search", 92, true, true, ["React", "TypeScript", "Azure", "AI"], ["Semantic Kernel"], new(29, 28, 14, 9, 9, 3)),
        new("job-2", "company-2", "source-2", "Atlassian", "Software Engineer, AI Platform", "Build reliable AI capabilities and TypeScript services for a better developer experience.", "Remote - India", "Remote", "Platform", "Full-time", DateTimeOffset.UtcNow.AddHours(-18).ToString("O"), DateTimeOffset.UtcNow.AddHours(-17).ToString("O"), "https://jobs.lever.co/atlassian/1002", "https://www.atlassian.com/company/careers", 88, true, true, ["TypeScript", "AI", "platform"], ["React", "Azure"], new(27, 25, 14, 10, 9, 3)),
        new("job-3", "company-3", "source-3", "Razorpay", "Backend Engineer - Payments", "Design scalable payment systems and APIs. Experience with distributed systems and SQL preferred.", "Bangalore, India", "On-site", "Engineering", "Full-time", DateTimeOffset.UtcNow.AddHours(-31).ToString("O"), DateTimeOffset.UtcNow.AddHours(-30).ToString("O"), "https://razorpay.com/jobs/1003", "https://razorpay.com/jobs", 68, false, false, ["SQL"], ["React", "TypeScript", "Azure"], new(18, 16, 14, 8, 3, 4))
    ];
    public Profile Profile { get; set; } = new("profile-1", ["Full Stack Developer", "Software Engineer", "AI Engineer"], ["React", "TypeScript", "C#", "SQL"], ["Azure", "Semantic Kernel", "Azure OpenAI"], 2, 6, ["Pune", "Mumbai", "Bangalore", "Remote"], "Any", ["AI", "GenAI", "LLM", "platform"], ["senior manager", "sales"], "omkar@example.com");
    public MatchingConfiguration Matching { get; set; } = new(70, 30, 30, 15, 10, 10, 5);
    public List<Notification> Notifications { get; } = [new("notification-1", "job-1", "Full Stack Software Engineer", "Microsoft", 92, "Match alert", DateTimeOffset.UtcNow.AddHours(-8).ToString("O"), "sent", null), new("notification-2", "job-2", "Software Engineer, AI Platform", "Atlassian", 88, "Match alert", DateTimeOffset.UtcNow.AddHours(-17).ToString("O"), "sent", null)];

    public object Dashboard() => new { stats = new { companies = Companies.Count(item => item.Enabled), activeSources = Sources.Count(item => item.Enabled), jobs = Jobs.Count, newJobs = Jobs.Count(item => DateTimeOffset.Parse(item.FirstSeenAt) > DateTimeOffset.UtcNow.AddDays(-1)), matchedJobs = Jobs.Count(item => item.IsMatch), notifiedJobs = Jobs.Count(item => item.Notified), failedSources = Sources.Count(item => item.Status is "failed" or "warning") }, recentMatches = Jobs.Where(item => item.IsMatch).OrderByDescending(item => item.Score).Take(5), sourceHealth = Sources };
    public Company AddCompany(CompanyInput input) { var company = new Company($"company-{Guid.NewGuid():N}"[..16], input.Name, input.Domain, Initials(input.Name), "#5B5CE2", true, 0, 0, DateTimeOffset.UtcNow.ToString("O")); Companies.Add(company); return company; }
    public Company? UpdateCompany(string id, CompanyUpdate input) { var index = Companies.FindIndex(item => item.Id == id); if (index < 0) return null; var current = Companies[index]; Companies[index] = current with { Name = input.Name ?? current.Name, Domain = input.Domain ?? current.Domain, Enabled = input.Enabled ?? current.Enabled, Initials = Initials(input.Name ?? current.Name) }; return Companies[index]; }
    public bool DeleteCompany(string id) => Companies.RemoveAll(item => item.Id == id) > 0;
    public JobSource? AddSource(SourceInput input) { var company = Companies.FirstOrDefault(item => item.Id == input.CompanyId); if (company is null || string.IsNullOrWhiteSpace(input.Name) || string.IsNullOrWhiteSpace(input.Url)) return null; var source = new JobSource($"source-{Guid.NewGuid():N}"[..15], company.Id, company.Name, input.Name, input.Type, input.Url, true, "never_run", "Never", 0, 0, null); Sources.Add(source); return source; }
    public JobSource? UpdateSource(string id, SourceUpdate input) { var index = Sources.FindIndex(item => item.Id == id); if (index < 0) return null; var current = Sources[index]; Sources[index] = current with { Name = input.Name ?? current.Name, Url = input.Url ?? current.Url, Enabled = input.Enabled ?? current.Enabled }; return Sources[index]; }
    public bool DeleteSource(string id) => Sources.RemoveAll(item => item.Id == id) > 0;
    public IEnumerable<Job> FilterJobs(string? search, string? status, string? location, string? workplaceType) { var result = Jobs.AsEnumerable(); if (!string.IsNullOrWhiteSpace(search)) result = result.Where(job => $"{job.Title} {job.Company} {job.Description}".Contains(search, StringComparison.OrdinalIgnoreCase)); if (!string.IsNullOrWhiteSpace(location)) result = result.Where(job => job.Location.Contains(location, StringComparison.OrdinalIgnoreCase)); if (!string.IsNullOrWhiteSpace(workplaceType)) result = result.Where(job => job.WorkplaceType == workplaceType); if (status == "matched") result = result.Where(job => job.IsMatch); if (status == "notified") result = result.Where(job => job.Notified); if (status == "new") result = result.Where(job => DateTimeOffset.Parse(job.FirstSeenAt) > DateTimeOffset.UtcNow.AddDays(-1)); return result; }
    public ScanResult Scan(IReadOnlyCollection<string> ids) { var selected = Sources.Where(source => ids.Contains(source.Id) && source.Enabled).ToList(); foreach (var source in selected) { var index = Sources.IndexOf(source); Sources[index] = source with { Status = "healthy", LastFetch = DateTimeOffset.UtcNow.ToString("O"), LastError = null }; } return new(selected.Count, selected.Sum(item => item.JobsFetched), selected.Count > 0 ? 2 : 0, selected.Count > 0 ? 2 : 0, 0); }
    private static string Initials(string name) => string.Concat(name.Split(' ', StringSplitOptions.RemoveEmptyEntries).Select(part => part[0])).ToUpperInvariant()[..Math.Min(2, name.Length)];
}

public record Company(string Id, string Name, string Domain, string Initials, string Color, bool Enabled, int SourceCount, int JobCount, string CreatedAt);
public record CompanyInput(string Name, string Domain);
public record CompanyUpdate(string? Name, string? Domain, bool? Enabled);
public record JobSource(string Id, string CompanyId, string CompanyName, string Name, string Type, string Url, bool Enabled, string Status, string LastFetch, int JobsFetched, int FailureCount, string? LastError);
public record SourceInput(string CompanyId, string Name, string Type, string Url);
public record SourceUpdate(string? Name, string? Url, bool? Enabled);
public record Job(string Id, string CompanyId, string SourceId, string Company, string Title, string Description, string Location, string WorkplaceType, string Department, string EmploymentType, string PostedDate, string FirstSeenAt, string ApplicationUrl, string SourceUrl, int Score, bool IsMatch, bool Notified, string[] MatchedSkills, string[] MissingSkills, Breakdown Breakdown);
public record Breakdown(int Role, int Skills, int Experience, int Location, int AiRelevance, int Freshness);
public record Profile(string Id, string[] Roles, string[] Skills, string[] Technologies, int MinYears, int MaxYears, string[] Locations, string WorkplacePreference, string[] IncludeKeywords, string[] ExcludeKeywords, string Email);
public record ProfileInput(string[] Roles, string[] Skills, string[] Technologies, int MinYears, int MaxYears, string[] Locations, string WorkplacePreference, string[] IncludeKeywords, string[] ExcludeKeywords, string Email);
public record MatchingConfiguration(int Threshold, int RoleWeight, int SkillsWeight, int ExperienceWeight, int LocationWeight, int AiWeight, int FreshnessWeight);
public record Notification(string Id, string JobId, string JobTitle, string Company, int Score, string Type, string SentAt, string Status, string? Error);
public record ScanResult(int SourcesScanned, int JobsFetched, int NewJobs, int MatchedJobs, int NotificationsSent);
