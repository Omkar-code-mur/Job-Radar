namespace JobRadar.Api.Sources;

public interface IJobSourceFetcher
{
    string SourceType { get; }

    Task<IReadOnlyList<Job>> FetchAsync(
        JobSource source,
        string companyName,
        CancellationToken cancellationToken);
}

public interface IJobSourceDiagnostics
{
    int MalformedRecordCount { get; }
    IReadOnlyList<string> Diagnostics { get; }
}