using System.Net;
using System.Text.RegularExpressions;

namespace JobRadar.Api.Sources.Greenhouse;

public static partial class GreenhouseNormalizer
{
    public static Job? Normalize(GreenhouseJob raw, string companyId, string sourceId, string company,
        string sourceUrl, DateTimeOffset now)
    {
        if (raw.Id <= 0 || string.IsNullOrWhiteSpace(raw.Title) || string.IsNullOrWhiteSpace(raw.AbsoluteUrl))
            return null;

        var description = StripHtml(raw.Content ?? string.Empty);
        var location = raw.Location?.Name?.Trim() ?? string.Empty;
        var workplace = DetectWorkplace(location, description);
        var department = string.Join(", ", (raw.Departments ?? []).Concat(raw.Offices ?? [])
            .Select(item => item.Name?.Trim()).Where(name => !string.IsNullOrWhiteSpace(name)));

        return new Job(
            $"job-greenhouse-{raw.Id}", companyId, sourceId, company, raw.Title.Trim(), description,
            location, workplace, department, string.Empty,
            (raw.UpdatedAt ?? now).ToString("O"), now.ToString("O"), raw.AbsoluteUrl, sourceUrl,
            0, false, false, [], [], new(0, 0, 0, 0, 0, 0));
    }

    private static string StripHtml(string value) => WhitespaceRegex().Replace(
        WebUtility.HtmlDecode(StripHtmlRegex().Replace(value, " ")), " ").Trim();

    private static string DetectWorkplace(string location, string description)
    {
        var text = $"{location} {description}";
        if (text.Contains("remote", StringComparison.OrdinalIgnoreCase)) return "Remote";
        if (text.Contains("hybrid", StringComparison.OrdinalIgnoreCase)) return "Hybrid";
        if (text.Contains("on-site", StringComparison.OrdinalIgnoreCase) || text.Contains("onsite", StringComparison.OrdinalIgnoreCase)) return "On-site";
        return "Unknown";
    }

    [GeneratedRegex("<[^>]+>", RegexOptions.Compiled)]
    private static partial Regex StripHtmlRegex();

    [GeneratedRegex("\\s+", RegexOptions.Compiled)]
    private static partial Regex WhitespaceRegex();
}