using System.Text.Json;
using Npgsql;
using JobRadar.Api.Sources;

public sealed class UserScopedJobRadarStore
{
    private readonly PostgresJobRadarStore _inner;
    private readonly string _connectionString;

    public UserScopedJobRadarStore(PostgresJobRadarStore inner, string connectionString)
    {
        _inner = inner;
        _connectionString = connectionString;
    }

    public async Task InitializeAsync(CancellationToken cancellationToken = default)
    {
        await _inner.InitializeAsync(cancellationToken);

        await using var connection = new NpgsqlConnection(_connectionString);
        await connection.OpenAsync(cancellationToken);
        const string sql = """
            create table if not exists job_matches (
                id uuid primary key,
                user_id uuid not null references users(id) on delete cascade,
                job_id text not null references jobs(id) on delete cascade,
                score integer not null,
                is_match boolean not null,
                matched_skills jsonb not null default '[]',
                missing_skills jsonb not null default '[]',
                breakdown jsonb not null default '{}',
                created_at timestamptz not null default now(),
                updated_at timestamptz not null default now(),
                unique(user_id, job_id)
            );
            create index if not exists ix_job_matches_user_score on job_matches(user_id, score desc);
            create index if not exists ix_job_matches_user_match on job_matches(user_id, is_match);
            """;
        await using var command = new NpgsqlCommand(sql, connection);
        await command.ExecuteNonQueryAsync(cancellationToken);
    }

    public Task<IReadOnlyList<Company>> GetCompaniesAsync(CancellationToken ct = default) => _inner.GetCompaniesAsync(ct);
    public Task<Company> AddCompanyAsync(CompanyInput input, CancellationToken ct = default) => _inner.AddCompanyAsync(input, ct);
    public Task<Company?> UpdateCompanyAsync(string id, CompanyUpdate input, CancellationToken ct = default) => _inner.UpdateCompanyAsync(id, input, ct);
    public Task<bool> DeleteCompanyAsync(string id, CancellationToken ct = default) => _inner.DeleteCompanyAsync(id, ct);
    public Task<IReadOnlyList<JobSource>> GetSourcesAsync(CancellationToken ct = default) => _inner.GetSourcesAsync(ct);
    public Task<JobSource?> AddSourceAsync(SourceInput input, CancellationToken ct = default) => _inner.AddSourceAsync(input, ct);
    public Task<JobSource?> UpdateSourceAsync(string id, SourceUpdate input, CancellationToken ct = default) => _inner.UpdateSourceAsync(id, input, ct);
    public Task<bool> DeleteSourceAsync(string id, CancellationToken ct = default) => _inner.DeleteSourceAsync(id, ct);
    public Task<Profile> GetProfileAsync(Guid userId, CancellationToken ct = default) => _inner.GetProfileAsync(userId, ct);
    public Task<Profile> SaveProfileAsync(Guid userId, ProfileInput input, CancellationToken ct = default) => _inner.SaveProfileAsync(userId, input, ct);
    public Task<MatchingConfiguration> GetMatchingAsync(Guid userId, CancellationToken ct = default) => _inner.GetMatchingAsync(userId, ct);
    public Task SaveMatchingAsync(Guid userId, MatchingConfiguration input, CancellationToken ct = default) => _inner.SaveMatchingAsync(userId, input, ct);
    public Task<IReadOnlyList<Notification>> GetNotificationsAsync(CancellationToken ct = default) => _inner.GetNotificationsAsync(ct);

    public async Task<IReadOnlyList<Job>> GetJobsAsync(Guid userId, string? search, string? status, string? location, string? workplaceType, CancellationToken cancellationToken = default)
    {
        var jobs = await _inner.GetJobsAsync(search, null, location, workplaceType, cancellationToken);
        var matches = await GetMatchesAsync(userId, jobs.Select(job => job.Id), cancellationToken);

        return jobs
            .Select(job => matches.TryGetValue(job.Id, out var match)
                ? job with
                {
                    Score = match.Score,
                    IsMatch = match.IsMatch,
                    MatchedSkills = match.MatchedSkills,
                    MissingSkills = match.MissingSkills,
                    Breakdown = match.Breakdown
                }
                : job with { Score = 0, IsMatch = false, MatchedSkills = [], MissingSkills = [], Breakdown = new Breakdown(0, 0, 0, 0, 0, 0) })
            .Where(job => status switch
            {
                "matched" => job.IsMatch,
                "notified" => job.Notified,
                "new" => DateTimeOffset.TryParse(job.FirstSeenAt, out var date) && date > DateTimeOffset.UtcNow.AddDays(-1),
                _ => true
            })
            .OrderByDescending(job => job.PostedDate)
            .ToArray();
    }

    public async Task<Job?> GetJobAsync(Guid userId, string id, CancellationToken cancellationToken = default)
        => (await GetJobsAsync(userId, null, null, null, null, cancellationToken)).FirstOrDefault(job => job.Id == id);

    public async Task<object> DashboardAsync(Guid userId, CancellationToken cancellationToken = default)
    {
        var companies = await _inner.GetCompaniesAsync(cancellationToken);
        var sources = await _inner.GetSourcesAsync(cancellationToken);
        var jobs = await GetJobsAsync(userId, null, null, null, null, cancellationToken);
        return new
        {
            stats = new
            {
                companies = companies.Count(item => item.Enabled),
                activeSources = sources.Count(item => item.Enabled),
                jobs = jobs.Count,
                newJobs = jobs.Count(item => DateTimeOffset.TryParse(item.FirstSeenAt, out var date) && date > DateTimeOffset.UtcNow.AddDays(-1)),
                matchedJobs = jobs.Count(item => item.IsMatch),
                notifiedJobs = jobs.Count(item => item.Notified),
                failedSources = sources.Count(item => item.Status is "failed" or "warning")
            },
            recentMatches = jobs.Where(item => item.IsMatch).OrderByDescending(item => item.Score).Take(5),
            sourceHealth = sources
        };
    }

    public async Task<ScanResult> ScanAsync(Guid userId, IReadOnlyCollection<string> ids, JobSourceFetcherFactory sourceFetcherFactory, CancellationToken cancellationToken = default)
    {
        var result = await _inner.ScanAsync(userId, ids, sourceFetcherFactory, cancellationToken);
        var jobs = await _inner.GetJobsAsync(null, null, null, null, cancellationToken);
        await UpsertMatchesAsync(userId, jobs, cancellationToken);
        return result;
    }

    private async Task<Dictionary<string, JobMatch>> GetMatchesAsync(Guid userId, IEnumerable<string> jobIds, CancellationToken cancellationToken)
    {
        var ids = jobIds.Distinct().ToArray();
        if (ids.Length == 0) return [];

        await using var connection = new NpgsqlConnection(_connectionString);
        await connection.OpenAsync(cancellationToken);
        const string sql = """
            select job_id, score, is_match, matched_skills, missing_skills, breakdown
            from job_matches
            where user_id = @user_id and job_id = any(@job_ids)
            """;
        await using var command = new NpgsqlCommand(sql, connection);
        command.Parameters.AddWithValue("user_id", userId);
        command.Parameters.AddWithValue("job_ids", ids);
        await using var reader = await command.ExecuteReaderAsync(cancellationToken);

        var result = new Dictionary<string, JobMatch>(StringComparer.Ordinal);
        while (await reader.ReadAsync(cancellationToken))
        {
            result[reader.GetString(0)] = new JobMatch(
                reader.GetInt32(1),
                reader.GetBoolean(2),
                Json<string[]>(reader.GetFieldValue<string>(3)),
                Json<string[]>(reader.GetFieldValue<string>(4)),
                Json<Breakdown>(reader.GetFieldValue<string>(5)));
        }
        return result;
    }

    private async Task UpsertMatchesAsync(Guid userId, IEnumerable<Job> jobs, CancellationToken cancellationToken)
    {
        await using var connection = new NpgsqlConnection(_connectionString);
        await connection.OpenAsync(cancellationToken);
        await using var transaction = await connection.BeginTransactionAsync(cancellationToken);

        foreach (var job in jobs)
        {
            const string sql = """
                insert into job_matches
                    (id, user_id, job_id, score, is_match, matched_skills, missing_skills, breakdown, created_at, updated_at)
                values
                    (@id, @user_id, @job_id, @score, @is_match, @matched_skills, @missing_skills, @breakdown, now(), now())
                on conflict (user_id, job_id) do update set
                    score = excluded.score,
                    is_match = excluded.is_match,
                    matched_skills = excluded.matched_skills,
                    missing_skills = excluded.missing_skills,
                    breakdown = excluded.breakdown,
                    updated_at = now();
                """;
            await using var command = new NpgsqlCommand(sql, connection, transaction);
            command.Parameters.AddWithValue("id", Guid.NewGuid());
            command.Parameters.AddWithValue("user_id", userId);
            command.Parameters.AddWithValue("job_id", job.Id);
            command.Parameters.AddWithValue("score", job.Score);
            command.Parameters.AddWithValue("is_match", job.IsMatch);
            command.Parameters.AddWithValue("matched_skills", NpgsqlTypes.NpgsqlDbType.Jsonb, JsonSerializer.Serialize(job.MatchedSkills));
            command.Parameters.AddWithValue("missing_skills", NpgsqlTypes.NpgsqlDbType.Jsonb, JsonSerializer.Serialize(job.MissingSkills));
            command.Parameters.AddWithValue("breakdown", NpgsqlTypes.NpgsqlDbType.Jsonb, JsonSerializer.Serialize(job.Breakdown));
            await command.ExecuteNonQueryAsync(cancellationToken);
        }

        await transaction.CommitAsync(cancellationToken);
    }

    private static T Json<T>(string value) => JsonSerializer.Deserialize<T>(value) ?? throw new InvalidOperationException("Invalid JSON in job_matches.");
    private sealed record JobMatch(int Score, bool IsMatch, string[] MatchedSkills, string[] MissingSkills, Breakdown Breakdown);
}
