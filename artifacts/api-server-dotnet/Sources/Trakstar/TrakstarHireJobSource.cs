using System.Net;
using System.Text.Json;
using System.Text.RegularExpressions;

namespace JobRadar.Api.Sources.Trakstar;

public sealed class TrakstarHireJobSource(
    HttpClient httpClient,
    ILogger<TrakstarHireJobSource> logger)
    : IJobSourceFetcher, IJobSourceDiagnostics
{
    public string SourceType => "TRAKSTAR_HIRE";
    public int MalformedRecordCount { get; private set; }
    public IReadOnlyList<string> Diagnostics { get; private set; } = [];

    public async Task<IReadOnlyList<Job>> FetchAsync(
        JobSource source,
        string companyName,
        CancellationToken cancellationToken)
    {
        MalformedRecordCount = 0;
        Diagnostics = [];

        var listingUrl = NormalizeListingUrl(source.Url);
        logger.LogInformation("Starting Trakstar Hire fetch for source {SourceId} ({CompanyName})", source.Id, companyName);

        using var listingResponse = await httpClient.GetAsync(listingUrl, cancellationToken);
        listingResponse.EnsureSuccessStatusCode();

        var listingHtml = await listingResponse.Content.ReadAsStringAsync(cancellationToken);
        var jobUrls = ExtractJobUrls(listingHtml, listingUrl)
            .Distinct(StringComparer.OrdinalIgnoreCase)
            .Take(100)
            .ToList();

        if (jobUrls.Count == 0)
        {
            Diagnostics = ["No public Trakstar job links were found on the careers page."];
            return [];
        }

        var jobs = new List<Job>();
        var diagnostics = new List<string>();

        foreach (var jobUrl in jobUrls)
        {
            try
            {
                using var response = await httpClient.GetAsync(jobUrl, cancellationToken);
                if (!response.IsSuccessStatusCode)
                {
                    diagnostics.Add($"Skipped {jobUrl}: HTTP {(int)response.StatusCode}.");
                    continue;
                }

                var html = await response.Content.ReadAsStringAsync(cancellationToken);
                var posting = ParseJobPosting(html);

                if (posting is null || string.IsNullOrWhiteSpace(posting.Title))
                {
                    diagnostics.Add($"Skipped malformed Trakstar job page: {jobUrl}");
                    continue;
                }

                var now = DateTimeOffset.UtcNow;
                var externalId = ExtractExternalId(jobUrl) ?? jobUrl;
                var postedDate = ParseDate(posting.DatePosted) ?? now;
                var description = StripHtml(posting.Description ?? string.Empty);
                var location = posting.Location ?? string.Empty;
                var workplace = DetectWorkplace(location, description);
                var department = posting.Department ?? string.Empty;
                var employmentType = posting.EmploymentType ?? string.Empty;

                jobs.Add(new Job(
                    $"job-trakstar-{source.CompanyId}-{source.Id}-{SanitizeId(externalId)}",
                    source.CompanyId,
                    source.Id,
                    externalId,
                    companyName,
                    posting.Title.Trim(),
                    description,
                    location.Trim(),
                    workplace,
                    department.Trim(),
                    employmentType.Trim(),
                    postedDate.ToString("O"),
                    now.ToString("O"),
                    now.ToString("O"),
                    jobUrl,
                    source.Url,
                    0,
                    false,
                    false,
                    [],
                    [],
                    new(0, 0, 0, 0, 0, 0)));
            }
            catch (OperationCanceledException) when (!cancellationToken.IsCancellationRequested)
            {
                diagnostics.Add($"Timed out while reading {jobUrl}.");
            }
            catch (Exception exception) when (exception is HttpRequestException or JsonException or InvalidOperationException)
            {
                diagnostics.Add($"Skipped {jobUrl}: {exception.Message}");
                logger.LogWarning(exception, "Failed to parse Trakstar job {JobUrl} for source {SourceId}", jobUrl, source.Id);
            }
        }

        MalformedRecordCount = jobUrls.Count - jobs.Count;
        Diagnostics = diagnostics;
        logger.LogInformation("Fetched {JobCount} valid jobs from Trakstar Hire source {SourceId}", jobs.Count, source.Id);
        return jobs;
    }

    private static string NormalizeListingUrl(string url)
    {
        if (!Uri.TryCreate(url, UriKind.Absolute, out var uri) ||
            !uri.Host.EndsWith(".hire.trakstar.com", StringComparison.OrdinalIgnoreCase))
            throw new InvalidOperationException("Trakstar Hire source URL must be a public *.hire.trakstar.com careers page.");

        return uri.GetLeftPart(UriPartial.Path).TrimEnd('/') + "/";
    }

    private static IReadOnlyList<string> ExtractJobUrls(string html, string listingUrl)
    {
        var baseUri = new Uri(listingUrl);
        var matches = Regex.Matches(
            html,
            @"href\s*=\s*[""']([^""']*/jobs/[^""'#?]+/?(?:\?[^""'#]*)?)[""']",
            RegexOptions.IgnoreCase);

        var urls = new List<string>();

        foreach (Match match in matches)
        {
            var href = WebUtility.HtmlDecode(match.Groups[1].Value).Trim();
            if (Uri.TryCreate(baseUri, href, out var uri) &&
                uri.Host.Equals(baseUri.Host, StringComparison.OrdinalIgnoreCase) &&
                uri.AbsolutePath.Contains("/jobs/", StringComparison.OrdinalIgnoreCase))
            {
                urls.Add(uri.GetLeftPart(UriPartial.Path).TrimEnd('/') + "/");
            }
        }

        return urls;
    }

    private static JobPosting? ParseJobPosting(string html)
    {
        foreach (Match match in Regex.Matches(
                     html,
                     @"<script[^>]+type\s*=\s*[""']application/ld\+json[""'][^>]*>(.*?)</script>",
                     RegexOptions.IgnoreCase | RegexOptions.Singleline))
        {
            var raw = WebUtility.HtmlDecode(match.Groups[1].Value).Trim();
            if (string.IsNullOrWhiteSpace(raw)) continue;

            try
            {
                using var doc = JsonDocument.Parse(raw);
                foreach (var node in EnumerateJsonLd(doc.RootElement))
                {
                    if (!node.TryGetProperty("@type", out var type)) continue;
                    var typeName = type.ValueKind == JsonValueKind.String ? type.GetString() : null;
                    if (!string.Equals(typeName, "JobPosting", StringComparison.OrdinalIgnoreCase)) continue;

                    return FromJsonLd(node);
                }
            }
            catch (JsonException)
            {
                // Some hosted pages contain more than one JSON-LD block; continue to the next one.
            }
        }

        var title = MatchText(html, @"<h1[^>]*>(.*?)</h1>") ?? MatchText(html, @"<title[^>]*>(.*?)</title>");
        var location = MatchText(html, @"(?:Location|Job Locations)\s*:?\s*</[^>]+>\s*<[^>]+>(.*?)</");
        return string.IsNullOrWhiteSpace(title)
            ? null
            : new JobPosting(WebUtility.HtmlDecode(title), null, location, null, null, null);
    }

    private static IEnumerable<JsonElement> EnumerateJsonLd(JsonElement root)
    {
        if (root.ValueKind == JsonValueKind.Array)
        {
            foreach (var item in root.EnumerateArray())
                if (item.ValueKind == JsonValueKind.Object) yield return item;
        }
        else if (root.ValueKind == JsonValueKind.Object)
        {
            if (root.TryGetProperty("@graph", out var graph) && graph.ValueKind == JsonValueKind.Array)
            {
                foreach (var item in graph.EnumerateArray())
                    if (item.ValueKind == JsonValueKind.Object) yield return item;
            }
            else yield return root;
        }
    }

    private static JobPosting FromJsonLd(JsonElement node)
    {
        var location = ReadLocation(node);
        var organization = node.TryGetProperty("hiringOrganization", out var org) &&
                           org.ValueKind == JsonValueKind.Object &&
                           org.TryGetProperty("name", out var orgName)
            ? orgName.GetString()
            : null;

        var department = node.TryGetProperty("department", out var dept)
            ? ReadName(dept)
            : null;

        return new JobPosting(
            ReadString(node, "title"),
            ReadString(node, "description"),
            location,
            ReadString(node, "employmentType"),
            ReadString(node, "datePosted"),
            department ?? organization);
    }

    private static string? ReadLocation(JsonElement node)
    {
        if (!node.TryGetProperty("jobLocation", out var value)) return null;
        var values = value.ValueKind == JsonValueKind.Array ? value.EnumerateArray().ToArray() : [value];
        var parts = new List<string>();

        foreach (var item in values)
        {
            if (item.ValueKind != JsonValueKind.Object) continue;
            if (!item.TryGetProperty("address", out var address) || address.ValueKind != JsonValueKind.Object) continue;

            foreach (var field in new[] { "streetAddress", "addressLocality", "addressRegion", "postalCode", "addressCountry" })
            {
                if (address.TryGetProperty(field, out var fieldValue) && fieldValue.ValueKind == JsonValueKind.String)
                {
                    var text = fieldValue.GetString();
                    if (!string.IsNullOrWhiteSpace(text)) parts.Add(text.Trim());
                }
            }
        }

        return parts.Count == 0 ? null : string.Join(", ", parts.Distinct(StringComparer.OrdinalIgnoreCase));
    }

    private static string? ReadName(JsonElement value)
    {
        if (value.ValueKind == JsonValueKind.String) return value.GetString();
        if (value.ValueKind == JsonValueKind.Object && value.TryGetProperty("name", out var name))
            return name.GetString();
        return null;
    }

    private static string? ReadString(JsonElement node, string property)
    {
        return node.TryGetProperty(property, out var value) && value.ValueKind == JsonValueKind.String
            ? value.GetString()
            : null;
    }

    private static DateTimeOffset? ParseDate(string? value)
        => DateTimeOffset.TryParse(value, out var parsed) ? parsed : null;

    private static string? ExtractExternalId(string url)
    {
        var match = Regex.Match(new Uri(url).AbsolutePath, @"/jobs/([^/]+)", RegexOptions.IgnoreCase);
        return match.Success ? match.Groups[1].Value : null;
    }

    private static string SanitizeId(string value)
        => Regex.Replace(value, @"[^a-zA-Z0-9_-]", "-");

    private static string? MatchText(string html, string pattern)
    {
        var match = Regex.Match(html, pattern, RegexOptions.IgnoreCase | RegexOptions.Singleline);
        return match.Success ? StripHtml(match.Groups[1].Value) : null;
    }

    private static string StripHtml(string value)
        => Regex.Replace(WebUtility.HtmlDecode(Regex.Replace(value, "<[^>]+>", " ")), @"\s+", " ").Trim();

    private static string DetectWorkplace(string location, string description)
    {
        var text = $"{location} {description}";
        if (text.Contains("remote", StringComparison.OrdinalIgnoreCase)) return "Remote";
        if (text.Contains("hybrid", StringComparison.OrdinalIgnoreCase)) return "Hybrid";
        if (text.Contains("on-site", StringComparison.OrdinalIgnoreCase) ||
            text.Contains("onsite", StringComparison.OrdinalIgnoreCase)) return "On-site";
        return "Unknown";
    }

    private sealed record JobPosting(
        string? Title,
        string? Description,
        string? Location,
        string? EmploymentType,
        string? DatePosted,
        string? Department);
}
