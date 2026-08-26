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

    public async Task<IReadOnlyList<Job>> FetchAsync(JobSource source, string companyName, CancellationToken cancellationToken)
    {
        var searchUrl = BuildSearchUrl(source.Url);
        logger.LogInformation("Starting Deloitte USI fetch for source {SourceId} from {SearchUrl}", source.Id, searchUrl);

        using var response = await httpClient.GetAsync(searchUrl, cancellationToken);
        response.EnsureSuccessStatusCode();
        var html = await response.Content.ReadAsStringAsync(cancellationToken);
        var now = DateTimeOffset.UtcNow;
        var seen = new HashSet<string>(StringComparer.Ordinal);
        var jobs = new List<Job>();

        foreach (Match match in JobLinkRegex().Matches(html))
        {
            var id = match.Groups["id"].Value;
            if (!seen.Add(id)) continue;

            var slug = WebUtility.HtmlDecode(match.Groups["slug"].Value);
            var title = ToTitle(slug);
            var applicationUrl = $"https://usijobs.deloitte.com/careersUSI/JobDetail/{slug}/{id}";
            var location = DetectLocation($"{slug} {html}");

            jobs.Add(new Job(
                $"job-deloitte-usi-{id}", source.CompanyId, source.Id, companyName, title, title,
                location, "Unknown", string.Empty, string.Empty, now.ToString("O"), now.ToString("O"),
                applicationUrl, source.Url, 0, false, false, [], [], new(0, 0, 0, 0, 0, 0)));
        }

        logger.LogInformation("Fetched {JobCount} Deloitte USI jobs from source {SourceId}", jobs.Count, source.Id);
        if (jobs.Count == 0)
            logger.LogWarning("Deloitte USI returned no recognizable job links for source {SourceId}", source.Id);
        return jobs;
    }

    private static string BuildSearchUrl(string sourceUrl)
    {
        var baseUrl = sourceUrl.TrimEnd('/');
        if (!baseUrl.Contains("SearchJobs", StringComparison.OrdinalIgnoreCase))
            baseUrl = "https://usijobs.deloitte.com/careersUSI/SearchJobs";
        return $"{baseUrl}/?jobRecordsPerPage={PageSize}&jobOffset=0";
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
