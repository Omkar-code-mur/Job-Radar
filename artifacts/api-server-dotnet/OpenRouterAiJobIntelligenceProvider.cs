using System.Net.Http.Headers;
using System.Text;
using System.Text.Json;

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
            throw new AiNotConfiguredException();

        var model = GetSetting("AI_MODEL", "OPENROUTER_MODEL");
        if (string.IsNullOrWhiteSpace(model))
            throw new AiNotConfiguredException("AI_MODEL is required when using OpenRouter.");

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
                    schema = Schema
                }
            }
        };

        var client = _httpClientFactory.CreateClient();
        using var request = new HttpRequestMessage(HttpMethod.Post, "https://openrouter.ai/api/v1/chat/completions");
        request.Headers.Authorization = new AuthenticationHeaderValue("Bearer", apiKey);
        request.Content = new StringContent(JsonSerializer.Serialize(payload), Encoding.UTF8, "application/json");

        using var response = await client.SendAsync(request, ct);
        if (!response.IsSuccessStatusCode)
            throw new AiProviderException($"OpenRouter returned HTTP {(int)response.StatusCode}.");

        try
        {
            using var document = JsonDocument.Parse(await response.Content.ReadAsStringAsync(ct));
            var content = document.RootElement
                .GetProperty("choices")[0]
                .GetProperty("message")
                .GetProperty("content")
                .GetString();

            if (string.IsNullOrWhiteSpace(content))
                throw new AiProviderException("OpenRouter returned an empty AI response.");

            return JsonSerializer.Deserialize<AiJobAnalysis>(
                content,
                new JsonSerializerOptions(JsonSerializerDefaults.Web))
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
        return $"Analyze this job for the candidate using only supplied evidence. Do not invent requirements or candidate experience. Return concise actionable conclusions. JOB: {job.Title} at {job.Company}; location {job.Location}; workplace {job.WorkplaceType}; employment {job.EmploymentType}; description {job.Description}. CANDIDATE: roles {string.Join(", ", profile.Roles)}; skills {string.Join(", ", profile.Skills)}; technologies {string.Join(", ", profile.Technologies)}; experience {profile.MinYears}-{profile.MaxYears}; locations {string.Join(", ", profile.Locations)}; workplace preference {profile.WorkplacePreference}; include {string.Join(", ", profile.IncludeKeywords)}; exclude {string.Join(", ", profile.ExcludeKeywords)}. Existing deterministic score {job.Score}%. Matched skills {string.Join(", ", job.MatchedSkills)}. Missing skills {string.Join(", ", job.MissingSkills)}.";
    }

    private static readonly object Schema = new
    {
        type = "object",
        properties = new
        {
            verdict = new { type = "string", @enum = new[] { "STRONG_FIT", "POSSIBLE_FIT", "WEAK_FIT" } },
            fitScore = new { type = "integer", minimum = 0, maximum = 100 },
            summary = new { type = "string" },
            strengths = new { type = "array", items = new { type = "string" } },
            gaps = new { type = "array", items = new { type = "string" } },
            concerns = new { type = "array", items = new { type = "string" } },
            interviewFocus = new { type = "array", items = new { type = "string" } },
            nextAction = new { type = "string" }
        },
        required = new[] { "verdict", "fitScore", "summary", "strengths", "gaps", "concerns", "interviewFocus", "nextAction" },
        additionalProperties = false
    };
}
