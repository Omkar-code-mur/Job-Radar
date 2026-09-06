public interface IAiJobIntelligenceProvider
{
    string Name { get; }
    Task<AiJobAnalysis> AnalyzeAsync(Job job, Profile profile, CancellationToken ct = default);
}
