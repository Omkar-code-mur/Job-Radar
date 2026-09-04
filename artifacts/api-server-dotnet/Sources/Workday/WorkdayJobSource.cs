using System.Net.Http.Headers;
using System.Text.Json;
using System.Text.Json.Serialization;
using JobRadar.Api.Sources;

namespace JobRadar.Api.Sources.Workday;

/// <summary>Reads publicly listed vacancies from a Workday career site's public CXS jobs endpoint.</summary>
public sealed class WorkdayJobSource(
    HttpClient httpClient,
    ILogger<WorkdayJobSource> logger) : IJobSourceFetcher
{
    public string SourceType => "WORKDAY_API";

    // Workday CXS career sites use 20 results per request.
    private const int PageSize = 20;
    private const int MaxPages = 20;

    public async Task<IReadOnlyList<Job>> FetchAsync(
        JobSource source,
        string companyName,
        CancellationToken cancellationToken)
    {
        if (string.IsNullOrWhiteSpace(source.Url))
            throw new ArgumentException("Workday source URL is required.", nameof(source));

        var endpoint = source.Url.TrimEnd('/');
        var now = DateTimeOffset.UtcNow;
        var jobs = new List<Job>();
        var seen = new HashSet<string>(StringComparer.OrdinalIgnoreCase);
        int? totalJobs = null;

        logger.LogInformation(
            "Starting Workday CXS fetch for source {SourceId} from {Endpoint}",
            source.Id,
            endpoint);

        for (var page = 0; page < MaxPages; page++)
        {
            cancellationToken.ThrowIfCancellationRequested();

            var request = new WorkdayJobsRequest(
                new Dictionary<string, object?>(),
                PageSize,
                page * PageSize,
                string.Empty);

            using var message = new HttpRequestMessage(HttpMethod.Post, endpoint)
            {
                Content = JsonContent.Create(request)
            };
            message.Headers.Accept.Add(new MediaTypeWithQualityHeaderValue("application/json"));
            message.Headers.AcceptLanguage.ParseAdd("en-US");

            using var response = await httpClient.SendAsync(message, cancellationToken);
            var responseBody = await response.Content.ReadAsStringAsync(cancellationToken);

            if (!response.IsSuccessStatusCode)
            {
                logger.LogError(
                    "Workday source {SourceId} returned HTTP {StatusCode}: {ResponseBody}",
                    source.Id,
                    (int)response.StatusCode,
                    responseBody.Length > 2000 ? responseBody[..2000] : responseBody);

                throw new HttpRequestException(
                    $"Workday CXS returned {(int)response.StatusCode} ({response.ReasonPhrase}). " +
                    (string.IsNullOrWhiteSpace(responseBody) ? "The response body was empty." : responseBody));
            }

            var payload = JsonSerializer.Deserialize<WorkdayJobsResponse>(responseBody);
            if (payload is null)
                throw new InvalidOperationException("Workday returned an empty or invalid JSON response.");

            if (page == 0)
                totalJobs = payload.Total;

            if (payload.JobPostings is null || payload.JobPostings.Count == 0)
            {
                logger.LogInformation(
                    "Workday source {SourceId} page {Page}: no jobs returned",
                    source.Id,
                    page + 1);
                break;
            }

            var pageJobs = 0;
            foreach (var posting in payload.JobPostings)
            {
                var title = posting.Title?.Trim();
                var externalPath = posting.ExternalPath?.Trim();
                if (string.IsNullOrWhiteSpace(title) || string.IsNullOrWhiteSpace(externalPath))
                    continue;

                var id = ExtractRequisitionId(externalPath) ?? externalPath;
                if (!seen.Add(id))
                    continue;

                var applicationUrl = BuildApplicationUrl(endpoint, externalPath);
                var location = posting.LocationsText?.Trim() ?? string.Empty;
                var postedDate = posting.PostedOn?.Trim() ?? string.Empty;
                var department = posting.BulletFields?.FirstOrDefault() ?? string.Empty;

                jobs.Add(new Job(
                    $"job-workday-{source.CompanyId}-{SanitizeId(id)}",
                    source.CompanyId,
                    source.Id,
                    companyName,
                    title,
                    title,
                    location,
                    "Unknown",
                    department,
                    string.Empty,
                    postedDate,
                    now.ToString("O"),
                    applicationUrl,
                    source.Url,
                    0,
                    false,
                    false,
                    [],
                    [],
                    new(0, 0, 0, 0, 0, 0)));

                pageJobs++;
            }

            logger.LogInformation(
                "Workday source {SourceId} page {Page}: returned {PageJobs} new jobs, total {TotalJobs}",
                source.Id,
                page + 1,
                pageJobs,
                jobs.Count);

            var nextOffset = (page + 1) * PageSize;
            if (pageJobs == 0 || (totalJobs.HasValue && totalJobs.Value > 0 && nextOffset >= totalJobs.Value))
                break;
        }

        logger.LogInformation(
            "Completed Workday fetch for source {SourceId}: {JobCount} jobs",
            source.Id,
            jobs.Count);

        return jobs;
    }

    private static string? ExtractRequisitionId(string externalPath)
    {
        var marker = externalPath.LastIndexOf('_');
        if (marker >= 0 && marker < externalPath.Length - 1)
            return externalPath[(marker + 1)..].TrimEnd('/');

        return externalPath.Trim('/').Split('/', StringSplitOptions.RemoveEmptyEntries).LastOrDefault();
    }

    private static string BuildApplicationUrl(string endpoint, string externalPath)
    {
        if (Uri.TryCreate(externalPath, UriKind.Absolute, out var absolute))
            return absolute.ToString();

        var uri = new Uri(endpoint, UriKind.Absolute);
        var segments = uri.AbsolutePath.Split('/', StringSplitOptions.RemoveEmptyEntries);
        var cxsIndex = Array.FindIndex(segments, segment => segment.Equals("cxs", StringComparison.OrdinalIgnoreCase));

        if (cxsIndex >= 0 && cxsIndex + 2 < segments.Length)
        {
            var tenant = segments[cxsIndex + 1];
            var site = segments[cxsIndex + 2];
            return $"{uri.GetLeftPart(UriPartial.Authority)}/en-US/{site}{externalPath}";
        }

        return $"{uri.GetLeftPart(UriPartial.Authority)}/{externalPath.TrimStart('/')}";
    }

    private static string SanitizeId(string value)
    {
        var sanitized = new string(value.Where(char.IsLetterOrDigit).ToArray());
        return string.IsNullOrWhiteSpace(sanitized) ? "unknown" : sanitized;
    }
}

internal sealed record WorkdayJobsRequest(
    [property: JsonPropertyName("appliedFacets")] Dictionary<string, object?> AppliedFacets,
    [property: JsonPropertyName("limit")] int Limit,
    [property: JsonPropertyName("offset")] int Offset,
    [property: JsonPropertyName("searchText")] string SearchText);
