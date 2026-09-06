using System.Net;
using System.Net.Http.Json;
using System.Text.Json;
using JobRadar.Api.Sources;

namespace JobRadar.Api.Sources.Greenhouse;

public sealed class GreenhouseJobSource(
    HttpClient httpClient,
    ILogger<GreenhouseJobSource> logger)
    : JobRadar.Api.Sources.IJobSourceFetcher, IJobSourceDiagnostics
{
    public string SourceType => "GREENHOUSE_API";
    public int MalformedRecordCount { get; private set; }
    public IReadOnlyList<string> Diagnostics { get; private set; } = [];
    private const int MaxRetries = 2;

    public async Task<IReadOnlyList<Job>> FetchAsync(JobSource source, string companyName, CancellationToken cancellationToken)
    {
        MalformedRecordCount = 0;
        Diagnostics = [];
        var boardToken = source.BoardToken;
        if (string.IsNullOrWhiteSpace(boardToken))
        {
            logger.LogWarning("Greenhouse scan skipped for source {SourceId}: board token is missing", source.Id);
            throw new InvalidOperationException("Greenhouse source requires a boardToken configuration value.");
        }

        var endpoint = $"https://boards-api.greenhouse.io/v1/boards/{Uri.EscapeDataString(boardToken)}/jobs?content=true";
        logger.LogInformation("Starting Greenhouse fetch for source {SourceId} ({CompanyName})", source.Id, companyName);
        for (var attempt = 0; attempt <= MaxRetries; attempt++)
        {
            try
            {
                using var response = await httpClient.GetAsync(endpoint, cancellationToken);
                if (IsTransient(response.StatusCode) && attempt < MaxRetries)
                {
                    logger.LogWarning(
                        "Greenhouse returned transient status {StatusCode} for source {SourceId}; retry {Attempt}/{MaxRetries}",
                        (int)response.StatusCode,
                        source.Id,
                        attempt + 1,
                        MaxRetries);
                    await Task.Delay(TimeSpan.FromMilliseconds(250 * (attempt + 1)), cancellationToken);
                    continue;
                }

                if (!response.IsSuccessStatusCode)
                    response.EnsureSuccessStatusCode();
                var payload = await response.Content.ReadFromJsonAsync<GreenhouseResponse>(cancellationToken: cancellationToken)
                    ?? throw new InvalidOperationException("Greenhouse returned an empty response.");
                var rawJobs = payload.Jobs ?? [];
                var jobs = rawJobs.Select(job => GreenhouseNormalizer.Normalize(job, source.CompanyId,
                    source.Id, companyName, source.Url, DateTimeOffset.UtcNow)).OfType<Job>().ToList();
                var malformedCount = rawJobs.Count - jobs.Count;
                MalformedRecordCount = malformedCount;
                Diagnostics = malformedCount == 0 ? [] : [$"Skipped {malformedCount} malformed Greenhouse record(s)."];
                if (malformedCount > 0)
                    logger.LogWarning("Skipped {MalformedCount} malformed Greenhouse records for source {SourceId}", malformedCount, source.Id);
                logger.LogInformation("Fetched {JobCount} valid jobs from Greenhouse source {SourceId}", jobs.Count, source.Id);
                return jobs;
            }
            catch (HttpRequestException exception) when (attempt < MaxRetries && exception.StatusCode is null)
            {
                logger.LogWarning(
                    exception,
                    "Greenhouse request failed for source {SourceId}; retry {Attempt}/{MaxRetries}",
                    source.Id,
                    attempt + 1,
                    MaxRetries);
                await Task.Delay(TimeSpan.FromMilliseconds(250 * (attempt + 1)), cancellationToken);
            }
            catch (JsonException exception)
            {
                logger.LogError(exception, "Greenhouse returned malformed JSON for source {SourceId}", source.Id);
                throw new InvalidOperationException("Greenhouse returned malformed JSON.", exception);
            }
        }

        logger.LogError("Greenhouse request exhausted retries for source {SourceId}", source.Id);
        throw new HttpRequestException("Greenhouse request failed after bounded retries.");
    }

    private static bool IsTransient(HttpStatusCode statusCode) => statusCode is HttpStatusCode.RequestTimeout
        or HttpStatusCode.TooManyRequests or HttpStatusCode.InternalServerError
        or HttpStatusCode.BadGateway or HttpStatusCode.ServiceUnavailable or HttpStatusCode.GatewayTimeout;
}
