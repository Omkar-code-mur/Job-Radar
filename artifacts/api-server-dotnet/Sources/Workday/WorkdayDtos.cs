namespace JobRadar.Api.Sources.Workday;

internal sealed record WorkdayJobsResponse(
    int Total,
    IReadOnlyList<WorkdayJobPosting> JobPostings);

internal sealed record WorkdayJobPosting(
    string? Title,
    string? ExternalPath,
    string? LocationsText,
    string? PostedOn,
    IReadOnlyList<string>? BulletFields);
