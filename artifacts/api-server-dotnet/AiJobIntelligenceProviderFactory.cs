public sealed class AiJobIntelligenceProviderFactory
{
    private readonly AiJobIntelligenceService _openAi;
    private readonly OpenRouterAiJobIntelligenceProvider _openRouter;
    private readonly IConfiguration _configuration;

    public AiJobIntelligenceProviderFactory(
        AiJobIntelligenceService openAi,
        OpenRouterAiJobIntelligenceProvider openRouter,
        IConfiguration configuration)
    {
        _openAi = openAi;
        _openRouter = openRouter;
        _configuration = configuration;
    }

    public IAiJobIntelligenceProvider Create()
    {
        var provider = _configuration["AI_PROVIDER"]
            ?? Environment.GetEnvironmentVariable("AI_PROVIDER")
            ?? "openai";

        return provider.Trim().ToLowerInvariant() switch
        {
            "openai" => _openAi,
            "openrouter" => _openRouter,
            _ => throw new AiNotConfiguredException($"Unsupported AI_PROVIDER '{provider}'. Use 'openai' or 'openrouter'.")
        };
    }
}
