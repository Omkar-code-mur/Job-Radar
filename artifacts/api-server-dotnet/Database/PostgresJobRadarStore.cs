using System.Text.Json;
using Npgsql;
using JobRadar.Api.Sources;

public sealed class PostgresJobRadarStore
{
    private readonly string connectionString;
    private readonly JsonSerializerOptions jsonOptions = new(JsonSerializerDefaults.Web);

    public PostgresJobRadarStore(string connectionString)
    {
        if (string.IsNullOrWhiteSpace(connectionString) || connectionString.Contains("REPLACE_WITH_NEW_PASSWORD", StringComparison.Ordinal))
            throw new InvalidOperationException("A valid Supabase connection string is required in ConnectionStrings:DefaultConnection or DATABASE_URL.");
        this.connectionString = connectionString;
    }

    public async Task InitializeAsync(CancellationToken cancellationToken = default)
    {
        await using var connection = await OpenAsync(cancellationToken);
        await using var command = new NpgsqlCommand("""
            create table if not exists companies (
                id text primary key, name text not null, domain text not null,
                initials text not null, color text not null, enabled boolean not null default true,
                created_at timestamptz not null
            );
            create table if not exists sources (
                id text primary key, company_id text not null references companies(id) on delete cascade,
                name text not null, type text not null, url text not null, board_token text,
                enabled boolean not null default true, status text not null default 'never_run',
                last_fetch text not null default 'Never', jobs_fetched integer not null default 0,
                failure_count integer not null default 0, last_error text
            );
            create table if not exists jobs (
                id text primary key, company_id text not null references companies(id) on delete cascade,
                source_id text not null references sources(id) on delete cascade, company text not null,
                title text not null, description text not null, location text not null,
                workplace_type text not null, department text not null, employment_type text not null,
                posted_date text not null, first_seen_at text not null, application_url text not null,
                source_url text not null, score integer not null default 0, is_match boolean not null default false,
                notified boolean not null default false, matched_skills jsonb not null default '[]',
                missing_skills jsonb not null default '[]', breakdown jsonb not null default '{}',
                unique(company_id, source_id, id)
            );
            create table if not exists profiles (
                id text primary key, roles jsonb not null, skills jsonb not null, technologies jsonb not null,
                min_years integer not null, max_years integer not null, locations jsonb not null,
                workplace_preference text not null, include_keywords jsonb not null, exclude_keywords jsonb not null,
                email text not null
            );
            create table if not exists matching_configurations (
                id integer primary key, threshold integer not null, role_weight integer not null,
                skills_weight integer not null, experience_weight integer not null, location_weight integer not null,
                ai_weight integer not null, freshness_weight integer not null
            );
            create table if not exists notifications (
                id text primary key, job_id text not null references jobs(id) on delete cascade,
                job_title text not null, company text not null, score integer not null, type text not null,
                sent_at text not null, status text not null, error text
            );
            create index if not exists ix_jobs_location on jobs(location);
            create index if not exists ix_jobs_posted_date on jobs(posted_date);
            create index if not exists ix_sources_company on sources(company_id);
            """, connection);
        await command.ExecuteNonQueryAsync(cancellationToken);
        await using var seed = new NpgsqlCommand("insert into matching_configurations (id, threshold, role_weight, skills_weight, experience_weight, location_weight, ai_weight, freshness_weight) values (1, 70, 30, 30, 15, 10, 10, 5) on conflict (id) do nothing", connection);
        await seed.ExecuteNonQueryAsync(cancellationToken);
    }

    public async Task<IReadOnlyList<Company>> GetCompaniesAsync(CancellationToken cancellationToken = default)
    {
        await using var connection = await OpenAsync(cancellationToken);
        await using var command = new NpgsqlCommand("select c.id, c.name, c.domain, c.initials, c.color, c.enabled, c.created_at, count(distinct s.id), count(distinct j.id) from companies c left join sources s on s.company_id = c.id left join jobs j on j.company_id = c.id group by c.id order by c.name", connection);
        await using var reader = await command.ExecuteReaderAsync(cancellationToken);
        var result = new List<Company>();
        while (await reader.ReadAsync(cancellationToken)) result.Add(new(reader.GetString(0), reader.GetString(1), reader.GetString(2), reader.GetString(3), reader.GetString(4), reader.GetBoolean(5), checked((int)reader.GetInt64(7)), checked((int)reader.GetInt64(8)), reader.GetFieldValue<DateTimeOffset>(6).ToString("O")));
        return result;
    }

    public async Task<Company> AddCompanyAsync(CompanyInput input, CancellationToken cancellationToken = default)
    {
        var company = new Company($"company-{Guid.NewGuid():N}"[..16], input.Name.Trim(), input.Domain.Trim(), Initials(input.Name), "#5B5CE2", true, 0, 0, DateTimeOffset.UtcNow.ToString("O"));
        await using var connection = await OpenAsync(cancellationToken);
        await using var command = new NpgsqlCommand("insert into companies (id,name,domain,initials,color,enabled,created_at) values (@id,@name,@domain,@initials,@color,@enabled,@created)", connection);
        Add(command, "id", company.Id); Add(command, "name", company.Name); Add(command, "domain", company.Domain); Add(command, "initials", company.Initials); Add(command, "color", company.Color); Add(command, "enabled", company.Enabled); Add(command, "created", DateTimeOffset.Parse(company.CreatedAt));
        await command.ExecuteNonQueryAsync(cancellationToken); return company;
    }

    public async Task<Company?> UpdateCompanyAsync(string id, CompanyUpdate input, CancellationToken cancellationToken = default)
    {
        var companies = await GetCompaniesAsync(cancellationToken); var current = companies.FirstOrDefault(item => item.Id == id); if (current is null) return null;
        var updated = current with { Name = input.Name ?? current.Name, Domain = input.Domain ?? current.Domain, Initials = Initials(input.Name ?? current.Name), Enabled = input.Enabled ?? current.Enabled };
        await using var connection = await OpenAsync(cancellationToken); await using var command = new NpgsqlCommand("update companies set name=@name, domain=@domain, initials=@initials, enabled=@enabled where id=@id", connection);
        Add(command, "id", id); Add(command, "name", updated.Name); Add(command, "domain", updated.Domain); Add(command, "initials", updated.Initials); Add(command, "enabled", updated.Enabled); await command.ExecuteNonQueryAsync(cancellationToken); return updated;
    }

    public async Task<bool> DeleteCompanyAsync(string id, CancellationToken cancellationToken = default) => await ExecuteBoolAsync("delete from companies where id=@id", id, cancellationToken);

    public async Task<IReadOnlyList<JobSource>> GetSourcesAsync(CancellationToken cancellationToken = default)
    {
        await using var connection = await OpenAsync(cancellationToken); await using var command = new NpgsqlCommand("select s.id,s.company_id,c.name,s.name,s.type,s.url,s.enabled,s.status,s.last_fetch,s.jobs_fetched,s.failure_count,s.last_error,s.board_token from sources s join companies c on c.id=s.company_id order by c.name,s.name", connection); await using var reader = await command.ExecuteReaderAsync(cancellationToken);
        var result = new List<JobSource>(); while (await reader.ReadAsync(cancellationToken)) result.Add(new(reader.GetString(0), reader.GetString(1), reader.GetString(2), reader.GetString(3), reader.GetString(4), reader.GetString(5), reader.GetBoolean(6), reader.GetString(7), reader.GetString(8), reader.GetInt32(9), reader.GetInt32(10), reader.IsDBNull(11) ? null : reader.GetString(11), reader.IsDBNull(12) ? null : reader.GetString(12))); return result;
    }

    public async Task<JobSource?> AddSourceAsync(SourceInput input, CancellationToken cancellationToken = default)
    {
        var companies = await GetCompaniesAsync(cancellationToken); var company = companies.FirstOrDefault(item => item.Id == input.CompanyId); if (company is null) return null;
        var token = input.BoardToken ?? ExtractBoardToken(input.Url); var source = new JobSource($"source-{Guid.NewGuid():N}"[..15], company.Id, company.Name, input.Name.Trim(), input.Type, input.Url.Trim(), true, "never_run", "Never", 0, 0, null, token);
        await using var connection = await OpenAsync(cancellationToken); await using var command = new NpgsqlCommand("insert into sources (id,company_id,name,type,url,board_token) values (@id,@company,@name,@type,@url,@token)", connection); Add(command, "id", source.Id); Add(command, "company", source.CompanyId); Add(command, "name", source.Name); Add(command, "type", source.Type); Add(command, "url", source.Url); Add(command, "token", (object?)source.BoardToken ?? DBNull.Value); await command.ExecuteNonQueryAsync(cancellationToken); return source;
    }

    public async Task<JobSource?> UpdateSourceAsync(string id, SourceUpdate input, CancellationToken cancellationToken = default)
    {
        var source = (await GetSourcesAsync(cancellationToken)).FirstOrDefault(item => item.Id == id); if (source is null) return null; var updated = source with { Name = input.Name ?? source.Name, Url = input.Url ?? source.Url, Enabled = input.Enabled ?? source.Enabled, BoardToken = source.BoardToken ?? ExtractBoardToken(input.Url ?? source.Url) };
        await using var connection = await OpenAsync(cancellationToken); await using var command = new NpgsqlCommand("update sources set name=@name,url=@url,enabled=@enabled,board_token=@token where id=@id", connection); Add(command, "id", id); Add(command, "name", updated.Name); Add(command, "url", updated.Url); Add(command, "enabled", updated.Enabled); Add(command, "token", (object?)updated.BoardToken ?? DBNull.Value); await command.ExecuteNonQueryAsync(cancellationToken); return updated;
    }

    public async Task<bool> DeleteSourceAsync(string id, CancellationToken cancellationToken = default) => await ExecuteBoolAsync("delete from sources where id=@id", id, cancellationToken);

    public async Task<IReadOnlyList<Job>> GetJobsAsync(string? search, string? status, string? location, string? workplaceType, CancellationToken cancellationToken = default)
    {
        var sql = "select id,company_id,source_id,company,title,description,location,workplace_type,department,employment_type,posted_date,first_seen_at,application_url,source_url,score,is_match,notified,matched_skills,missing_skills,breakdown from jobs where 1=1"; if (!string.IsNullOrWhiteSpace(search)) sql += " and (title ilike @search or company ilike @search or description ilike @search)"; if (!string.IsNullOrWhiteSpace(location)) sql += " and location ilike @location"; if (!string.IsNullOrWhiteSpace(workplaceType)) sql += " and workplace_type=@workplace"; if (status == "matched") sql += " and is_match=true"; if (status == "notified") sql += " and notified=true"; if (status == "new") sql += " and first_seen_at::timestamptz > now() - interval '1 day'"; sql += " order by posted_date desc";
        await using var connection = await OpenAsync(cancellationToken); await using var command = new NpgsqlCommand(sql, connection); if (!string.IsNullOrWhiteSpace(search)) Add(command, "search", $"%{search}%"); if (!string.IsNullOrWhiteSpace(location)) Add(command, "location", $"%{location}%"); if (!string.IsNullOrWhiteSpace(workplaceType)) Add(command, "workplace", workplaceType); return await ReadJobsAsync(command, cancellationToken);
    }

    public async Task<Job?> GetJobAsync(string id, CancellationToken cancellationToken = default) => (await GetJobsAsync(null, null, null, null, cancellationToken)).FirstOrDefault(item => item.Id == id);

    public async Task UpsertJobsAsync(IEnumerable<Job> jobs, CancellationToken cancellationToken = default)
    {
        await using var connection = await OpenAsync(cancellationToken); await using var transaction = await connection.BeginTransactionAsync(cancellationToken);
        foreach (var job in jobs)
        {
            await using var command = new NpgsqlCommand("insert into jobs (id,company_id,source_id,company,title,description,location,workplace_type,department,employment_type,posted_date,first_seen_at,application_url,source_url,score,is_match,notified,matched_skills,missing_skills,breakdown) values (@id,@company_id,@source_id,@company,@title,@description,@location,@workplace,@department,@employment,@posted,@first_seen,@application,@source,@score,@match,@notified,@matched,@missing,@breakdown) on conflict (id) do update set title=excluded.title,description=excluded.description,location=excluded.location,workplace_type=excluded.workplace_type,department=excluded.department,posted_date=excluded.posted_date,application_url=excluded.application_url,source_url=excluded.source_url,matched_skills=excluded.matched_skills,missing_skills=excluded.missing_skills,breakdown=excluded.breakdown", connection, transaction);
            Add(command,"id",job.Id); Add(command,"company_id",job.CompanyId); Add(command,"source_id",job.SourceId); Add(command,"company",job.Company); Add(command,"title",job.Title); Add(command,"description",job.Description); Add(command,"location",job.Location); Add(command,"workplace",job.WorkplaceType); Add(command,"department",job.Department); Add(command,"employment",job.EmploymentType); Add(command,"posted",job.PostedDate); Add(command,"first_seen",job.FirstSeenAt); Add(command,"application",job.ApplicationUrl); Add(command,"source",job.SourceUrl); Add(command,"score",job.Score); Add(command,"match",job.IsMatch); Add(command,"notified",job.Notified); Add(command,"matched",JsonSerializer.Serialize(job.MatchedSkills,jsonOptions)); Add(command,"missing",JsonSerializer.Serialize(job.MissingSkills,jsonOptions)); Add(command,"breakdown",JsonSerializer.Serialize(job.Breakdown,jsonOptions)); await command.ExecuteNonQueryAsync(cancellationToken);
        }
        await transaction.CommitAsync(cancellationToken);
    }

    public async Task<Profile> GetProfileAsync(CancellationToken cancellationToken = default)
    {
        await using var connection = await OpenAsync(cancellationToken); await using var command = new NpgsqlCommand("select id,roles,skills,technologies,min_years,max_years,locations,workplace_preference,include_keywords,exclude_keywords,email from profiles limit 1", connection); await using var reader = await command.ExecuteReaderAsync(cancellationToken); if (!await reader.ReadAsync(cancellationToken)) return new("profile-1",[],[],[],0,0,[],"Any",[],[],string.Empty); return new(reader.GetString(0),Json<string[]>(reader.GetString(1)),Json<string[]>(reader.GetString(2)),Json<string[]>(reader.GetString(3)),reader.GetInt32(4),reader.GetInt32(5),Json<string[]>(reader.GetString(6)),reader.GetString(7),Json<string[]>(reader.GetString(8)),Json<string[]>(reader.GetString(9)),reader.GetString(10));
    }

    public async Task<Profile> SaveProfileAsync(ProfileInput input, CancellationToken cancellationToken = default)
    {
        var profile = new Profile("profile-1",input.Roles,input.Skills,input.Technologies,input.MinYears,input.MaxYears,input.Locations,input.WorkplacePreference,input.IncludeKeywords,input.ExcludeKeywords,input.Email); await using var connection = await OpenAsync(cancellationToken); await using var command = new NpgsqlCommand("insert into profiles (id,roles,skills,technologies,min_years,max_years,locations,workplace_preference,include_keywords,exclude_keywords,email) values ('profile-1',@roles,@skills,@technologies,@min,@max,@locations,@workplace,@include,@exclude,@email) on conflict (id) do update set roles=excluded.roles,skills=excluded.skills,technologies=excluded.technologies,min_years=excluded.min_years,max_years=excluded.max_years,locations=excluded.locations,workplace_preference=excluded.workplace_preference,include_keywords=excluded.include_keywords,exclude_keywords=excluded.exclude_keywords,email=excluded.email",connection); AddJson(command,"roles",profile.Roles); AddJson(command,"skills",profile.Skills); AddJson(command,"technologies",profile.Technologies); Add(command,"min",profile.MinYears); Add(command,"max",profile.MaxYears); AddJson(command,"locations",profile.Locations); Add(command,"workplace",profile.WorkplacePreference); AddJson(command,"include",profile.IncludeKeywords); AddJson(command,"exclude",profile.ExcludeKeywords); Add(command,"email",profile.Email); await command.ExecuteNonQueryAsync(cancellationToken); return profile;
    }

    public async Task<MatchingConfiguration> GetMatchingAsync(CancellationToken cancellationToken = default)
    { await using var connection=await OpenAsync(cancellationToken); await using var command=new NpgsqlCommand("select threshold,role_weight,skills_weight,experience_weight,location_weight,ai_weight,freshness_weight from matching_configurations where id=1",connection); await using var reader=await command.ExecuteReaderAsync(cancellationToken); if(!await reader.ReadAsync(cancellationToken)) return new(70,30,30,15,10,10,5); return new(reader.GetInt32(0),reader.GetInt32(1),reader.GetInt32(2),reader.GetInt32(3),reader.GetInt32(4),reader.GetInt32(5),reader.GetInt32(6)); }
    public async Task SaveMatchingAsync(MatchingConfiguration input, CancellationToken cancellationToken = default) { await using var connection=await OpenAsync(cancellationToken); await using var command=new NpgsqlCommand("update matching_configurations set threshold=@threshold,role_weight=@role,skills_weight=@skills,experience_weight=@experience,location_weight=@location,ai_weight=@ai,freshness_weight=@freshness where id=1",connection); Add(command,"threshold",input.Threshold); Add(command,"role",input.RoleWeight); Add(command,"skills",input.SkillsWeight); Add(command,"experience",input.ExperienceWeight); Add(command,"location",input.LocationWeight); Add(command,"ai",input.AiWeight); Add(command,"freshness",input.FreshnessWeight); await command.ExecuteNonQueryAsync(cancellationToken); }

    public async Task<object> DashboardAsync(CancellationToken cancellationToken = default) { var companies=await GetCompaniesAsync(cancellationToken); var sources=await GetSourcesAsync(cancellationToken); var jobs=await GetJobsAsync(null,null,null,null,cancellationToken); return new { stats=new { companies=companies.Count(item=>item.Enabled), activeSources=sources.Count(item=>item.Enabled), jobs=jobs.Count, newJobs=jobs.Count(item=>DateTimeOffset.TryParse(item.FirstSeenAt,out var date)&&date>DateTimeOffset.UtcNow.AddDays(-1)), matchedJobs=jobs.Count(item=>item.IsMatch), notifiedJobs=jobs.Count(item=>item.Notified), failedSources=sources.Count(item=>item.Status is "failed" or "warning") }, recentMatches=jobs.Where(item=>item.IsMatch).OrderByDescending(item=>item.Score).Take(5), sourceHealth=sources }; }
    public async Task<ScanResult> ScanAsync(
        IReadOnlyCollection<string> ids,
        JobSourceFetcherFactory sourceFetcherFactory,
        CancellationToken cancellationToken = default)
    {
        var sources = (await GetSourcesAsync(cancellationToken))
            .Where(item =>
                ids.Contains(item.Id) &&
                item.Enabled)
            .ToList();

        var fetched = 0;
        var added = 0;

        var profile = await GetProfileAsync(cancellationToken);
        var matching = await GetMatchingAsync(cancellationToken);

        foreach (var source in sources)
        {
            try
            {
                var fetcher = sourceFetcherFactory.Get(source.Type);

                var jobs = await fetcher.FetchAsync(
                    source,
                    source.CompanyName,
                    cancellationToken);

                jobs = jobs
                    .Select(job => ScoreJob(job, profile, matching))
                    .ToList();

                var existing = (await GetJobsAsync(
                        null,
                        null,
                        null,
                        null,
                        cancellationToken))
                    .Select(item => item.Id)
                    .ToHashSet();

                await UpsertJobsAsync(jobs, cancellationToken);

                fetched += jobs.Count;
                added += jobs.Count(item => !existing.Contains(item.Id));

                await UpdateSourceHealthAsync(
                    source.Id,
                    jobs.Count,
                    null,
                    cancellationToken);
            }
            catch (NotSupportedException exception)
            {
                await UpdateSourceHealthAsync(
                    source.Id,
                    0,
                    exception.Message,
                    cancellationToken);

                // Unsupported source types should not crash the entire scan.
                continue;
            }
            catch (Exception exception)
            {
                await UpdateSourceHealthAsync(
                    source.Id,
                    0,
                    exception.Message,
                    cancellationToken);

                throw;
            }
        }

        return new(
            sources.Count,
            fetched,
            added,
            added,
            0);
    }

    public async Task<IReadOnlyList<Notification>> GetNotificationsAsync(CancellationToken cancellationToken = default) { await using var connection=await OpenAsync(cancellationToken); await using var command=new NpgsqlCommand("select id,job_id,job_title,company,score,type,sent_at,status,error from notifications order by sent_at desc",connection); await using var reader=await command.ExecuteReaderAsync(cancellationToken); var result=new List<Notification>(); while(await reader.ReadAsync(cancellationToken)) result.Add(new(reader.GetString(0),reader.GetString(1),reader.GetString(2),reader.GetString(3),reader.GetInt32(4),reader.GetString(5),reader.GetString(6),reader.GetString(7),reader.IsDBNull(8)?null:reader.GetString(8))); return result; }

    private async Task UpdateSourceHealthAsync(string id,int jobs,string? error,CancellationToken cancellationToken) { await using var connection=await OpenAsync(cancellationToken); await using var command=new NpgsqlCommand("update sources set status=@status,last_fetch=@fetch,jobs_fetched=@jobs,last_error=@error where id=@id",connection); Add(command,"id",id); Add(command,"status",error is null?"healthy":"failed"); Add(command,"fetch",DateTimeOffset.UtcNow.ToString("O")); Add(command,"jobs",jobs); Add(command,"error",(object?)error??DBNull.Value); await command.ExecuteNonQueryAsync(cancellationToken); }
    private static Job ScoreJob(Job job, Profile profile, MatchingConfiguration matching)
    {
        var text = $"{job.Title} {job.Description} {job.Department} {job.Location}".ToLowerInvariant();
        var excluded = profile.ExcludeKeywords.Any(keyword => Contains(text, keyword));
        var matchedSkills = profile.Skills.Concat(profile.Technologies).Where(keyword => Contains(text, keyword)).Distinct(StringComparer.OrdinalIgnoreCase).ToArray();
        var roleMatched = profile.Roles.Any(role => Contains(text, role));
        var locationMatched = profile.Locations.Length == 0 || profile.Locations.Any(location => Contains(job.Location, location));
        var includeMatched = profile.IncludeKeywords.Length == 0 || profile.IncludeKeywords.Any(keyword => Contains(text, keyword));
        var roleScore = roleMatched ? matching.RoleWeight : 0;
        var skillsScore = profile.Skills.Length + profile.Technologies.Length == 0 ? matching.SkillsWeight : (int)Math.Round(matching.SkillsWeight * (double)matchedSkills.Length / (profile.Skills.Length + profile.Technologies.Length));
        var locationScore = locationMatched ? matching.LocationWeight : 0;
        var score = excluded ? 0 : roleScore + skillsScore + locationScore + (includeMatched ? matching.AiWeight : 0) + matching.FreshnessWeight;
        var breakdown = new Breakdown(roleScore, skillsScore, 0, locationScore, includeMatched ? matching.AiWeight : 0, matching.FreshnessWeight);
        return job with { Score = Math.Min(score, 100), IsMatch = !excluded && score >= matching.Threshold, MatchedSkills = matchedSkills, MissingSkills = profile.Skills.Where(keyword => !Contains(text, keyword)).ToArray(), Breakdown = breakdown };
    }
    private static bool Contains(string text, string value) => !string.IsNullOrWhiteSpace(value) && text.Contains(value.Trim(), StringComparison.OrdinalIgnoreCase);
    private async Task<List<Job>> ReadJobsAsync(NpgsqlCommand command,CancellationToken cancellationToken) { await using var reader=await command.ExecuteReaderAsync(cancellationToken); var result=new List<Job>(); while(await reader.ReadAsync(cancellationToken)) result.Add(new(reader.GetString(0),reader.GetString(1),reader.GetString(2),reader.GetString(3),reader.GetString(4),reader.GetString(5),reader.GetString(6),reader.GetString(7),reader.GetString(8),reader.GetString(9),reader.GetString(10),reader.GetString(11),reader.GetString(12),reader.GetString(13),reader.GetInt32(14),reader.GetBoolean(15),reader.GetBoolean(16),Json<string[]>(reader.GetFieldValue<string>(17)),Json<string[]>(reader.GetFieldValue<string>(18)),Json<Breakdown>(reader.GetFieldValue<string>(19)))); return result; }
    private async Task<NpgsqlConnection> OpenAsync(CancellationToken cancellationToken) { var connection=new NpgsqlConnection(connectionString); await connection.OpenAsync(cancellationToken); return connection; }
    private async Task<bool> ExecuteBoolAsync(string sql,string id,CancellationToken cancellationToken) { await using var connection=await OpenAsync(cancellationToken); await using var command=new NpgsqlCommand(sql,connection); Add(command,"id",id); return await command.ExecuteNonQueryAsync(cancellationToken)>0; }
    private static void Add(NpgsqlCommand command,string name,object value)
    {
        if (name is "matched" or "missing" or "breakdown")
            command.Parameters.AddWithValue(name, NpgsqlTypes.NpgsqlDbType.Jsonb, value);
        else
            command.Parameters.AddWithValue(name, value);
    }
    private static void AddJson<T>(NpgsqlCommand command,string name,T value) => command.Parameters.AddWithValue(name,NpgsqlTypes.NpgsqlDbType.Jsonb,JsonSerializer.Serialize(value));
    private T Json<T>(string value) => JsonSerializer.Deserialize<T>(value,jsonOptions)!;
private static string Initials(string name)
{
    var parts = name
        .Split(' ', StringSplitOptions.RemoveEmptyEntries);

    if (parts.Length == 0)
        return "?";

    var initials = string.Concat(parts.Select(part => part[0]))
        .ToUpperInvariant();

    return initials.Length > 2
        ? initials[..2]
        : initials;
}    private static string? ExtractBoardToken(string url) { if(!Uri.TryCreate(url,UriKind.Absolute,out var uri)||!uri.Host.Equals("boards.greenhouse.io",StringComparison.OrdinalIgnoreCase)) return null; var token=uri.AbsolutePath.Trim('/').Split('/')[0]; return string.IsNullOrWhiteSpace(token)?null:token; }
}
