using System.Net.Http.Headers;
using System.Text;
using System.Text.Json;

namespace JobRadar.Api;

public sealed class OpenRouterAiJobIntelligenceProvider : IAiJobIntelligenceProvider
{
    private readonly IHttpClientFactory _httpClientFactory;
    private readonly IConfiguration _configuration;

    public OpenRouterAiJobIntelligenceProvider(
        IHttpClientFactory httpClientFactory,
        IConfiguration configuration)
    {
        _httpClientFactory = httpClientFactory;
        _configuration = configuration;
    }

    public string Name => "openrouter-chat-completions";

    public async Task<AiJobAnalysis> AnalyzeAsync(Job job, Profile profile, CancellationToken ct = default)
    {
        var apiKey = GetSetting("AI_API_KEY", "OPENROUTER_API_KEY");
        if (string.IsNullOrWhiteSpace(apiKey))
        {
            throw new AiNotConfiguredException("OpenRouter AI is not configured.");
        }

        var model = GetSetting("AI_MODEL", "OPENROUTER_MODEL");
        if (string.IsNullOrWhiteSpace(model))
        {
            throw new AiNotConfiguredException("AI_MODEL is required when using OpenRouter.");
        }

        var payload = new
        {
            model,
            messages = new[]
            {
                new
                {
                    role = "user",
                    content = BuildPrompt(job, profile)
                }
            },
            response_format = new
            {
                type = "json_schema",
                json_schema = new
                {
                    name = "job_intelligence",
                    strict = true,
                    schema = BuildSchema()
                }
            }
        };

        var client = _httpClientFactory.CreateClient();
        using var request = new HttpRequestMessage(HttpMethod.Post, "https://openrouter.ai/api/v1/chat/completions");
        request.Headers.Authorization = new AuthenticationHeaderValue("Bearer", apiKey);
        request.Headers.Accept.Add(new MediaTypeWithQualityHeaderValue("application/json"));
        request.Content = new StringContent(
            JsonSerializer.Serialize(payload),
            Encoding.UTF8,
            "application/json");

        using var response = await client.SendAsync(request, ct);
        var responseBody = await response.Content.ReadAsStringAsync(ct);

        if (!response.IsSuccessStatusCode)
        {
            throw new AiProviderException($"OpenRouter returned HTTP {(int)response.StatusCode}.");
        }

        try
        {
            using var document = JsonDocument.Parse(responseBody);
            var content = document.RootElement
                .GetProperty("choices")[0]
                .GetProperty("message")
                .GetProperty("content")
                .GetString();

            if (string.IsNullOrWhiteSpace(content))
            {
                throw new AiProviderException("OpenRouter returned an empty AI response.");
            }

            return JsonSerializer.Deserialize<AiJobAnalysis>(content)
                   ?? throw new AiProviderException("OpenRouter returned invalid job intelligence JSON.");
        }
        catch (AiProviderException)
        {
            throw;
        }
        catch (Exception ex) when (ex is JsonException || ex is KeyNotFoundException || ex is IndexOutOfRangeException)
        {
            throw new AiProviderException("OpenRouter returned an unexpected response shape.", ex);
        }
    }

    private string? GetSetting(string primary, string fallback)
        => _configuration[primary]
           ?? Environment.GetEnvironmentVariable(primary)
           ?? _configuration[fallback]
           ?? Environment.GetEnvironmentVariable(fallback);

    private static string BuildPrompt(Job job, Profile profile)
    {
        return $"""
            Analyze this job against the candidate profile.

            Return only the requested structured JSON. Do not invent candidate experience or requirements.
            Base the analysis on the supplied evidence and distinguish demonstrated skills from merely discussed skills.

            Candidate profile:
            Target roles: {JsonSerializer.Serialize(profile.TargetRoles)}
            Skills: {JsonSerializer.Serialize(profile.Skills)}
            Preferred locations: {JsonSerializer.Serialize(profile.PreferredLocations)}
            Experience: {JsonSerializer.Serialize(profile.Experience)}
            Projects: {JsonSerializer.Serialize(profile.Projects)}

            Job:
            Title: {job.Title}
            Company: {job.Company}
            Location: {job.Location}
            Description: {job.Description}
            """;
    }

    private static object BuildSchema() => new
    {
        type = "object",
        additionalProperties = false,
        properties = new
        {
            Verdict = new { type = "string", @enum = new[] { "STRONG_FIT", "POSSIBLE_FIT", "WEAK_FIT" } },
            FitScore = new { type = "integer", minimum = 0, maximum = 100 },
            Summary = new { type = "string" },
            Strengths = new { type = "array", items = new { type = "string" } },
            Gaps = new { type = "array", items = new { type = "string" } },
            Concerns = new { type = "array", items = new { type = "string" } },
            InterviewFocus = new { type = "array", items = new { type = "string" } },
            NextAction = new { type = "string" }
        },
        required = new[]
        {
            "Verdict", "FitScore", "Summary", "Strengths", "Gaps", "Concerns", "InterviewFocus", "NextAction"
        }
    };
}
