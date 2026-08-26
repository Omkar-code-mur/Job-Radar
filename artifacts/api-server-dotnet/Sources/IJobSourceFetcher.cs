namespace JobRadar.Api.Sources;

public interface IJobSourceFetcher
{
    string SourceType { get; }

    Task<IReadOnlyList<Job>> FetchAsync(
        JobSource source,
        string companyName,
        CancellationToken cancellationToken);
}