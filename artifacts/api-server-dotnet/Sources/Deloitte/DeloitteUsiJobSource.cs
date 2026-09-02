using System.Net;
using JobRadar.Api.Sources;
using System.Text.RegularExpressions;

namespace JobRadar.Api.Sources.Deloitte;

/// <summary>Reads publicly listed vacancies from Deloitte USI's Avature careers search.</summary>
public sealed partial class DeloitteUsiJobSource(
    HttpClient httpClient,
    ILogger<DeloitteUsiJobSource> logger)
    : IJobSourceFetcher
{
    public string SourceType => "DELOITTE_USI";

    private const int PageSize = 50;
    private const int MaxPages = 20;
    private const int PageDelayMilliseconds = 300;

    public async Task<IReadOnlyList<Job>> FetchAsync(
        JobSource source,
        string companyName,
        CancellationToken cancellationToken)
    {
        var baseUrl = BuildBaseUrl(source.Url);
        var now = DateTimeOffset.UtcNow;
        var seen = new HashSet<string>(StringComparer.Ordinal);
        var jobs = new List<Job>();

        logger.LogInformation(
            "Starting Deloitte USI paginated fetch for source {SourceId} from {SearchUrl}",
            source.Id,
            baseUrl);

        for (var page = 0; page < MaxPages; page++)
        {
            cancellationToken.ThrowIfCancellationRequested();

            var offset = page * PageSize;
            var searchUrl = $"{baseUrl}/?jobRecordsPerPage={PageSize}&jobOffset={offset}";

            if (page > 0)
                await Task.Delay(PageDelayMilliseconds, cancellationToken);

            using var response = await httpClient.GetAsync(searchUrl, cancellationToken);
            response.EnsureSuccessStatusCode();

            var html = await response.Content.ReadAsStringAsync(cancellationToken);
            var pageJobs = 0;

            foreach (Match match in JobLinkRegex().Matches(html))
            {
                var id = match.Groups["id"].Value;
                if (!seen.Add(id))
                    continue;

                var slug = WebUtility.HtmlDecode(match.Groups["slug"].Value);
                var title = ToTitle(slug);
                var applicationUrl = $"https://usijobs.deloitte.com/careersUSI/JobDetail/{slug}/{id}";
                var location = DetectLocation($"{slug} {html}");

                jobs.Add(new Job(
                    $"job-deloitte-usi-{id}", source.CompanyId, source.Id, companyName, title, title,
                    location, "Unknown", string.Empty, string.Empty, now.ToString("O"), now.ToString("O"),
                    applicationUrl, source.Url, 0, false, false, [], [], new(0, 0, 0, 0, 0, 0)));

                pageJobs++;
            }

            logger.LogInformation(
                "Deloitte USI page {Page} offset {Offset}: found {PageJobCount} new jobs, total {TotalJobs}",
                page + 1,
                offset,
                pageJobs,
                jobs.Count);

            if (pageJobs == 0 || pageJobs < PageSize)
                break;
        }

        logger.LogInformation(
            "Completed Deloitte USI fetch for source {SourceId}: {JobCount} jobs across up to {MaxPages} pages",
            source.Id,
            jobs.Count,
            MaxPages);

        if (jobs.Count == 0)
            logger.LogWarning(
                "Deloitte USI returned no recognizable job links for source {SourceId}",
                source.Id);

        return jobs;
    }

    private static string BuildBaseUrl(string sourceUrl)
    {
        var baseUrl = sourceUrl.TrimEnd('/');
        if (!baseUrl.Contains("SearchJobs", StringComparison.OrdinalIgnoreCase))
            baseUrl = "https://usijobs.deloitte.com/careersUSI/SearchJobs";
        return baseUrl;
    }

    private static string ToTitle(string slug) => WebUtility.UrlDecode(slug)
        .Replace('-', ' ')
        .Replace('_', ' ')
        .Trim();

    private static string DetectLocation(string text)
    {
        string[] locations = ["Pune", "Bengaluru", "Bangalore", "Hyderabad", "Mumbai", "Gurugram", "Gurgaon", "Delhi", "Chennai", "Kolkata", "Noida"];
        return locations.FirstOrDefault(location => text.Contains(location, StringComparison.OrdinalIgnoreCase)) ?? string.Empty;
    }

    [GeneratedRegex("(?:https?://usijobs\\.deloitte\\.com)?(?:/en_US)?/careersUSI/JobDetail/(?<slug>[^\\\"?#<]+?)/(?<id>\\d+)", RegexOptions.IgnoreCase | RegexOptions.Compiled)]
    private static partial Regex JobLinkRegex();
}
