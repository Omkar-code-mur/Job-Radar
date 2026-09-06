using System.Net.Http.Headers;
using System.Text;
using System.Text.Json;

public sealed class AiJobIntelligenceService : IAiJobIntelligenceProvider
{
    private readonly IHttpClientFactory _clients;
    private readonly IConfiguration _configuration;

    public AiJobIntelligenceService(IHttpClientFactory clients, IConfiguration configuration)
    {
        _clients = clients;
        _configuration = configuration;
    }

    public string Name => "openai-responses";

    public async Task<AiJobAnalysis> AnalyzeAsync(Job job, Profile profile, CancellationToken ct = default)
    {
        var apiKey = _configuration["AI_API_KEY"]
            ?? Environment.GetEnvironmentVariable("AI_API_KEY")
            ?? _configuration["OPENAI_API_KEY"]
            ?? Environment.GetEnvironmentVariable("OPENAI_API_KEY");

        if (string.IsNullOrWhiteSpace(apiKey))
            throw new AiNotConfiguredException();

        var model = _configuration["AI_MODEL"]
            ?? Environment.GetEnvironmentVariable("AI_MODEL")
            ?? _configuration["OPENAI_MODEL"]
            ?? Environment.GetEnvironmentVariable("OPENAI_MODEL")
            ?? "gpt-5.6-luna";

        var baseUrl = _configuration["AI_BASE_URL"]
            ?? Environment.GetEnvironmentVariable("AI_BASE_URL")
            ?? "https://api.openai.com";

        var prompt = $"Analyze this job for the candidate using only supplied evidence. Do not invent requirements or candidate experience. Return concise actionable conclusions. JOB: {job.Title} at {job.Company}; location {job.Location}; workplace {job.WorkplaceType}; employment {job.EmploymentType}; description {job.Description}. CANDIDATE: roles {string.Join(", ", profile.Roles)}; skills {string.Join(", ", profile.Skills)}; technologies {string.Join(", ", profile.Technologies)}; experience {profile.MinYears}-{profile.MaxYears}; locations {string.Join(", ", profile.Locations)}; workplace preference {profile.WorkplacePreference}; include {string.Join(", ", profile.IncludeKeywords)}; exclude {string.Join(", ", profile.ExcludeKeywords)}. Existing deterministic score {job.Score}%. Matched skills {string.Join(", ", job.MatchedSkills)}. Missing skills {string.Join(", ", job.MissingSkills)}.";

        var payload = new
        {
            model,
            input = prompt,
            text = new
            {
                format = new
                {
                    type = "json_schema",
                    name = "job_intelligence",
                    strict = true,
                    schema = Schema
                }
            }
        };

        using var request = new HttpRequestMessage(HttpMethod.Post, $"{baseUrl.TrimEnd('/')}/v1/responses");
        request.Headers.Authorization = new AuthenticationHeaderValue("Bearer", apiKey);
        request.Content = new StringContent(JsonSerializer.Serialize(payload), Encoding.UTF8, "application/json");

        using var response = await _clients.CreateClient().SendAsync(request, ct);
        if (!response.IsSuccessStatusCode)
            throw new AiProviderException($"AI provider returned {(int)response.StatusCode}.");

        using var document = JsonDocument.Parse(await response.Content.ReadAsStringAsync(ct));
        var text = ExtractOutputText(document.RootElement);

        return JsonSerializer.Deserialize<AiJobAnalysis>(text, new JsonSerializerOptions(JsonSerializerDefaults.Web))
            ?? throw new AiProviderException("AI provider returned an empty analysis.");
    }

    private static string ExtractOutputText(JsonElement root)
    {
        if (!root.TryGetProperty("output", out var output))
            throw new AiProviderException("AI provider returned no output.");

        foreach (var item in output.EnumerateArray())
        {
            if (!item.TryGetProperty("content", out var content))
                continue;

            foreach (var part in content.EnumerateArray())
            {
                if (part.TryGetProperty("text", out var text) && text.ValueKind == JsonValueKind.String)
                    return text.GetString()!;
            }
        }

        throw new AiProviderException("AI provider returned no text output.");
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

public sealed class AiNotConfiguredException : Exception { }
public sealed class AiProviderException : Exception
{
    public AiProviderException(string message) : base(message) { }
}

public record AiJobAnalysis(
    string Verdict,
    int FitScore,
    string Summary,
    string[] Strengths,
    string[] Gaps,
    string[] Concerns,
    string[] InterviewFocus,
    string NextAction);
