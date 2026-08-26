namespace JobRadar.Api.Sources;

public sealed class JobSourceFetcherFactory(
    IEnumerable<IJobSourceFetcher> fetchers)
{
    private readonly IReadOnlyDictionary<string, IJobSourceFetcher> _fetchers =
        fetchers.ToDictionary(
            fetcher => fetcher.SourceType,
            StringComparer.OrdinalIgnoreCase);

    public IJobSourceFetcher Get(string sourceType)
    {
        if (_fetchers.TryGetValue(sourceType, out var fetcher))
        {
            return fetcher;
        }

        throw new NotSupportedException(
            $"No job source fetcher is registered for source type '{sourceType}'.");
    }
}