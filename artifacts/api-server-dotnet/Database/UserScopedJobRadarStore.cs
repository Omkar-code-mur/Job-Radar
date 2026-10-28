using System.Security.Cryptography;
using System.Text;
using System.Text.Json;
using System.Text.RegularExpressions;
using Npgsql;
using JobRadar.Api.Sources;

public sealed class UserScopedJobRadarStore
{
    private readonly PostgresJobRadarStore _inner;
    private readonly string _connectionString;
    private static readonly JsonSerializerOptions JsonOptions = new(JsonSerializerDefaults.Web);

    // This small, deterministic vocabulary provides explainable matching without an AI key.
    // Add aliases carefully: similar-looking technologies are not always interchangeable.
    private static readonly string[][] SkillAliases =
    [
        [".net", "dotnet", "asp.net", "asp.net core"],
        ["c#", "c sharp"],
        ["javascript", "js"],
        ["typescript", "ts"],
        ["react", "react.js", "reactjs"],
        ["node.js", "nodejs", "node"],
        ["sql server", "microsoft sql server", "mssql"],
        ["postgresql", "postgres", "postgres db"],
        ["mongodb", "mongo db"],
        ["azure ai search", "cognitive search"],
        ["azure openai", "azure openai service"],
        ["azure functions", "azure function app"],
        ["cosmos db", "azure cosmos db", "cosmosdb"],
        ["semantic kernel", "microsoft semantic kernel"],
        ["rest api", "restful api", "rest apis"],
        ["machine learning", "ml"],
        ["generative ai", "genai", "gen ai"],
        ["agentic ai", "ai agents", "multi-agent"]
    ];

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
                match_reasons jsonb not null default '[]',
                profile_fingerprint text not null default '',
                job_fingerprint text not null default '',
                created_at timestamptz not null default now(),
                updated_at timestamptz not null default now(),
                unique(user_id, job_id)
            );
            alter table job_matches add column if not exists match_reasons jsonb not null default '[]';
            alter table job_matches add column if not exists profile_fingerprint text not null default '';
            alter table job_matches add column if not exists job_fingerprint text not null default '';
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

    public async Task<Profile> SaveProfileAsync(Guid userId, ProfileInput input, CancellationToken ct = default)
    {
        var profile = await _inner.SaveProfileAsync(userId, input, ct);
        await RecalculateMatchesAsync(userId, ct);
        return profile;
    }

    public Task<MatchingConfiguration> GetMatchingAsync(Guid userId, CancellationToken ct = default) => _inner.GetMatchingAsync(userId, ct);

    public async Task SaveMatchingAsync(Guid userId, MatchingConfiguration input, CancellationToken ct = default)
    {
        await _inner.SaveMatchingAsync(userId, input, ct);
        await RecalculateMatchesAsync(userId, ct);
    }

    public Task<IReadOnlyList<Notification>> GetNotificationsAsync(CancellationToken ct = default) => _inner.GetNotificationsAsync(ct);

    public async Task<IReadOnlyList<Job>> GetJobsAsync(Guid userId, string? search, string? status, string? location, string? workplaceType, CancellationToken cancellationToken = default)
    {
        // Fetch without the shared-table "matched" filter: match state is user-specific.
        var jobs = await _inner.GetJobsAsync(search, null, location, workplaceType, cancellationToken);
        if (jobs.Count == 0) return [];

        var profile = await _inner.GetProfileAsync(userId, cancellationToken);
        var config = await _inner.GetMatchingAsync(userId, cancellationToken);
        var profileFingerprint = ProfileFingerprint(profile, config);
        var matches = await GetMatchesAsync(userId, jobs.Select(job => job.Id), cancellationToken);
        var stale = jobs.Where(job =>
            !matches.TryGetValue(job.Id, out var match)
            || match.ProfileFingerprint != profileFingerprint
            || match.JobFingerprint != JobFingerprint(job)).ToArray();

        if (stale.Length > 0)
        {
            await UpsertMatchesAsync(userId, stale.Select(job => Score(job, profile, config, profileFingerprint)), profileFingerprint, cancellationToken);
            matches = await GetMatchesAsync(userId, jobs.Select(job => job.Id), cancellationToken);
        }

        return jobs
            .Select(job => matches.TryGetValue(job.Id, out var match)
                ? job with
                {
                    Score = match.Score,
                    IsMatch = match.IsMatch,
                    MatchedSkills = match.MatchedSkills,
                    MissingSkills = match.MissingSkills,
                    Breakdown = match.Breakdown,
                    MatchReasons = match.MatchReasons
                }
                : job with { Score = 0, IsMatch = false, MatchedSkills = [], MissingSkills = [], MatchReasons = [], Breakdown = new Breakdown(0, 0, 0, 0, 0, 0) })
            .Where(job => status switch
            {
                "matched" => job.IsMatch,
                "notified" => job.Notified,
                "new" => DateTimeOffset.TryParse(job.FirstSeenAt, out var date) && date > DateTimeOffset.UtcNow.AddDays(-1),
                _ => true
            })
            .OrderByDescending(job => job.Score)
            .ThenByDescending(job => job.FirstSeenAt)
            .ThenByDescending(job => job.PostedDate)
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
        // New or updated job descriptions are rescored for this user after ingestion.
        await RecalculateMatchesAsync(userId, cancellationToken);
        return result;
    }

    public async Task<int> RecalculateMatchesAsync(Guid userId, CancellationToken cancellationToken = default)
    {
        var jobs = await _inner.GetJobsAsync(null, null, null, null, cancellationToken);
        if (jobs.Count == 0) return 0;
        var profile = await _inner.GetProfileAsync(userId, cancellationToken);
        var config = await _inner.GetMatchingAsync(userId, cancellationToken);
        var fingerprint = ProfileFingerprint(profile, config);
        await UpsertMatchesAsync(userId, jobs.Select(job => Score(job, profile, config, fingerprint)), fingerprint, cancellationToken);
        return jobs.Count;
    }

    private async Task<Dictionary<string, JobMatch>> GetMatchesAsync(Guid userId, IEnumerable<string> jobIds, CancellationToken cancellationToken)
    {
        var ids = jobIds.Distinct().ToArray();
        if (ids.Length == 0) return [];
        await using var connection = new NpgsqlConnection(_connectionString);
        await connection.OpenAsync(cancellationToken);
        const string sql = """
            select job_id, score, is_match, matched_skills, missing_skills, breakdown,
                   match_reasons, profile_fingerprint, job_fingerprint
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
                reader.GetInt32(1), reader.GetBoolean(2),
                Json<string[]>(reader.GetFieldValue<string>(3)),
                Json<string[]>(reader.GetFieldValue<string>(4)),
                Json<Breakdown>(reader.GetFieldValue<string>(5)),
                Json<string[]>(reader.GetFieldValue<string>(6)),
                reader.GetString(7), reader.GetString(8));
        }
        return result;
    }

    private async Task UpsertMatchesAsync(Guid userId, IEnumerable<Job> jobs, string profileFingerprint, CancellationToken cancellationToken)
    {
        await using var connection = new NpgsqlConnection(_connectionString);
        await connection.OpenAsync(cancellationToken);
        await using var transaction = await connection.BeginTransactionAsync(cancellationToken);
        foreach (var job in jobs)
        {
            const string sql = """
                insert into job_matches
                    (id, user_id, job_id, score, is_match, matched_skills, missing_skills, breakdown,
                     match_reasons, profile_fingerprint, job_fingerprint, created_at, updated_at)
                values
                    (@id, @user_id, @job_id, @score, @is_match, @matched_skills, @missing_skills, @breakdown,
                     @match_reasons, @profile_fingerprint, @job_fingerprint, now(), now())
                on conflict (user_id, job_id) do update set
                    score = excluded.score,
                    is_match = excluded.is_match,
                    matched_skills = excluded.matched_skills,
                    missing_skills = excluded.missing_skills,
                    breakdown = excluded.breakdown,
                    match_reasons = excluded.match_reasons,
                    profile_fingerprint = excluded.profile_fingerprint,
                    job_fingerprint = excluded.job_fingerprint,
                    updated_at = now();
                """;
            await using var command = new NpgsqlCommand(sql, connection, transaction);
            command.Parameters.AddWithValue("id", Guid.NewGuid());
            command.Parameters.AddWithValue("user_id", userId);
            command.Parameters.AddWithValue("job_id", job.Id);
            command.Parameters.AddWithValue("score", job.Score);
            command.Parameters.AddWithValue("is_match", job.IsMatch);
            command.Parameters.AddWithValue("matched_skills", NpgsqlTypes.NpgsqlDbType.Jsonb, JsonSerializer.Serialize(job.MatchedSkills, JsonOptions));
            command.Parameters.AddWithValue("missing_skills", NpgsqlTypes.NpgsqlDbType.Jsonb, JsonSerializer.Serialize(job.MissingSkills, JsonOptions));
            command.Parameters.AddWithValue("breakdown", NpgsqlTypes.NpgsqlDbType.Jsonb, JsonSerializer.Serialize(job.Breakdown, JsonOptions));
            command.Parameters.AddWithValue("match_reasons", NpgsqlTypes.NpgsqlDbType.Jsonb, JsonSerializer.Serialize(job.MatchReasons ?? [], JsonOptions));
            command.Parameters.AddWithValue("profile_fingerprint", profileFingerprint);
            command.Parameters.AddWithValue("job_fingerprint", JobFingerprint(job));
            await command.ExecuteNonQueryAsync(cancellationToken);
        }
        await transaction.CommitAsync(cancellationToken);
    }

    private static Job Score(Job job, Profile profile, MatchingConfiguration config, string profileFingerprint)
    {
        var title = job.Title ?? string.Empty;
        var description = job.Description ?? string.Empty;
        var searchable = $"{title} {description} {job.Department}".ToLowerInvariant();
        var candidateSkills = profile.Skills.Concat(profile.Technologies).Where(s => !string.IsNullOrWhiteSpace(s)).Distinct(StringComparer.OrdinalIgnoreCase).ToArray();
        var detectedJobSkills = SkillAliases
            .Where(group => group.Any(alias => ContainsTerm(searchable, alias)))
            .Select(group => group[0])
            .ToArray();

        var matchedSkills = detectedJobSkills.Where(skill => candidateSkills.Any(candidate => SameSkill(candidate, skill))).ToArray();
        var missingSkills = detectedJobSkills.Except(matchedSkills, StringComparer.OrdinalIgnoreCase).ToArray();

        var roleScore = profile.Roles.Length == 0 ? 50 : Math.Clamp((int)Math.Round(
            100d * profile.Roles.Count(role => ContainsTerm(title, role) || ContainsTerm(role, title)) / profile.Roles.Length), 0, 100);
        if (profile.Roles.Length > 0 && roleScore == 0 && profile.Roles.Any(role => role.Split(' ', StringSplitOptions.RemoveEmptyEntries).Any(token => token.Length > 2 && ContainsTerm(title, token))))
            roleScore = 55;

        var skillsScore = detectedJobSkills.Length == 0
            ? (candidateSkills.Length == 0 ? 35 : 55)
            : (int)Math.Round(100d * matchedSkills.Length / detectedJobSkills.Length);

        var experienceScore = ExperienceScore(profile, searchable);
        var locationScore = LocationScore(profile, job);
        var freshnessScore = FreshnessScore(job);
        // The existing ai_weight is retained for configuration compatibility. This component is a
        // deterministic description-evidence score, not a claim that an LLM ran.
        var includeHits = profile.IncludeKeywords.Count(keyword => ContainsTerm(searchable, keyword));
        var includeScore = profile.IncludeKeywords.Length == 0 ? 50 : (int)Math.Round(100d * includeHits / profile.IncludeKeywords.Length);
        var evidenceScore = detectedJobSkills.Length == 0 ? includeScore : (int)Math.Round((includeScore + skillsScore) / 2d);

        var weighted = roleScore * config.RoleWeight
            + skillsScore * config.SkillsWeight
            + experienceScore * config.ExperienceWeight
            + locationScore * config.LocationWeight
            + evidenceScore * config.AiWeight
            + freshnessScore * config.FreshnessWeight;
        var score = Math.Clamp((int)Math.Round(weighted / 100d), 0, 100);

        var excluded = profile.ExcludeKeywords.FirstOrDefault(keyword => ContainsTerm(searchable, keyword));
        var isMatch = excluded is null && score >= config.Threshold;
        var reasons = new List<string>();
        if (profile.Roles.Length > 0) reasons.Add(roleScore >= 70 ? "Role/title aligns with your target roles." : "Role/title is only a partial match.");
        if (matchedSkills.Length > 0) reasons.Add($"Matched skills: {string.Join(", ", matchedSkills)}.");
        if (missingSkills.Length > 0) reasons.Add($"Potential skill gaps: {string.Join(", ", missingSkills)}.");
        if (experienceScore >= 70) reasons.Add("Experience range appears compatible or no conflicting requirement was found.");
        if (locationScore >= 70) reasons.Add("Location or work-mode preference appears compatible.");
        if (excluded is not null) reasons.Add($"Excluded because it contains your excluded keyword: {excluded}.");
        else reasons.Add(isMatch ? $"Score meets your {config.Threshold}% threshold." : $"Score is below your {config.Threshold}% threshold.");

        return job with
        {
            Score = score,
            IsMatch = isMatch,
            MatchedSkills = matchedSkills,
            MissingSkills = missingSkills,
            Breakdown = new Breakdown(roleScore, skillsScore, experienceScore, locationScore, evidenceScore, freshnessScore),
            MatchReasons = reasons.ToArray()
        };
    }

    private static int ExperienceScore(Profile profile, string text)
    {
        if (profile.MinYears == 0 && profile.MaxYears == 0) return 60;
        var match = Regex.Match(text, @"(?<!\d)(\d{1,2})\s*(?:-|to)\s*(\d{1,2})\s*(?:years?|yrs?)\b|(?:minimum|min\.?|at least)\s*(\d{1,2})\s*(?:years?|yrs?)\b", RegexOptions.IgnoreCase);
        if (!match.Success) return 65;
        int requiredMin = match.Groups[3].Success ? int.Parse(match.Groups[3].Value) : int.Parse(match.Groups[1].Value);
        int requiredMax = match.Groups[2].Success ? int.Parse(match.Groups[2].Value) : requiredMin + 10;
        if (profile.MaxYears >= requiredMin && profile.MinYears <= requiredMax) return 100;
        if (profile.MaxYears < requiredMin) return Math.Clamp(100 - (requiredMin - profile.MaxYears) * 20, 0, 60);
        return 65;
    }

    private static int LocationScore(Profile profile, Job job)
    {
        var preference = profile.WorkplacePreference?.Trim() ?? "Any";
        var workplace = job.WorkplaceType ?? "Unknown";
        var modeScore = preference.Equals("Any", StringComparison.OrdinalIgnoreCase) || string.IsNullOrWhiteSpace(preference)
            ? 75
            : ContainsTerm(workplace, preference) || ContainsTerm(preference, workplace) ? 100
            : workplace.Equals("Unknown", StringComparison.OrdinalIgnoreCase) ? 60 : 20;
        if (profile.Locations.Length == 0) return modeScore;
        var location = job.Location ?? string.Empty;
        var locationHit = profile.Locations.Any(candidate => ContainsTerm(location, candidate) || ContainsTerm(candidate, location));
        return (int)Math.Round((modeScore + (locationHit ? 100 : 25)) / 2d);
    }

    private static int FreshnessScore(Job job)
    {
        if (!DateTimeOffset.TryParse(job.LastSeenAt, out var date)) date = DateTimeOffset.TryParse(job.FirstSeenAt, out var first) ? first : DateTimeOffset.MinValue;
        var age = DateTimeOffset.UtcNow - date;
        if (age <= TimeSpan.FromDays(1)) return 100;
        if (age <= TimeSpan.FromDays(7)) return 80;
        if (age <= TimeSpan.FromDays(30)) return 55;
        return 25;
    }

    private static bool SameSkill(string candidate, string detected)
        => SkillAliases.Any(group => group.Contains(detected, StringComparer.OrdinalIgnoreCase)
            && group.Any(alias => ContainsTerm(candidate, alias)))
           || Normalize(candidate) == Normalize(detected);

    private static bool ContainsTerm(string text, string term)
    {
        if (string.IsNullOrWhiteSpace(text) || string.IsNullOrWhiteSpace(term)) return false;
        var normalizedText = Normalize(text);
        var normalizedTerm = Normalize(term);
        if (normalizedText.Length == 0 || normalizedTerm.Length == 0) return false;
        return Regex.IsMatch(normalizedText, $@"(?<![a-z0-9]){Regex.Escape(normalizedTerm)}(?![a-z0-9])", RegexOptions.IgnoreCase);
    }

    private static string Normalize(string value)
        => Regex.Replace(value.ToLowerInvariant().Replace("c sharp", "c#").Replace("dotnet", ".net"), @"[^a-z0-9#+.]+", " ").Trim();

    private static string ProfileFingerprint(Profile profile, MatchingConfiguration config)
        => Hash(JsonSerializer.Serialize(new { profile.Roles, profile.Skills, profile.Technologies, profile.MinYears, profile.MaxYears, profile.Locations, profile.WorkplacePreference, profile.IncludeKeywords, profile.ExcludeKeywords, config }, JsonOptions));

    private static string JobFingerprint(Job job)
        => Hash(JsonSerializer.Serialize(new { job.Title, job.Description, job.Location, job.WorkplaceType, job.Department, job.EmploymentType, job.PostedDate, job.LastSeenAt }, JsonOptions));

    private static string Hash(string value) => Convert.ToHexString(SHA256.HashData(Encoding.UTF8.GetBytes(value)));

    private static T Json<T>(string value) => JsonSerializer.Deserialize<T>(value, JsonOptions) ?? throw new InvalidOperationException("Invalid JSON in job_matches.");

    private sealed record JobMatch(
        int Score, bool IsMatch, string[] MatchedSkills, string[] MissingSkills, Breakdown Breakdown,
        string[] MatchReasons, string ProfileFingerprint, string JobFingerprint);
}
