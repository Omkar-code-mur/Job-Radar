using Npgsql;
using System.Text.Json;

public sealed class UserWorkspaceStore
{
    private readonly string _connectionString;

    public UserWorkspaceStore(string connectionString) => _connectionString = connectionString;

    public async Task InitializeAsync(CancellationToken ct = default)
    {
        await using var connection = new NpgsqlConnection(_connectionString);
        await connection.OpenAsync(ct);
        const string sql = """
            create table if not exists applications (
                id uuid primary key,
                user_id uuid not null references users(id) on delete cascade,
                job_id text not null references jobs(id) on delete cascade,
                status text not null default 'NEW' check (status in ('NEW','SHORTLISTED','APPLIED','INTERVIEW','REJECTED','CLOSED')),
                applied_at timestamptz null,
                resume_version text null,
                notes text null,
                follow_up_at timestamptz null,
                created_at timestamptz not null default now(),
                updated_at timestamptz not null default now(),
                unique(user_id, job_id)
            );
            create index if not exists ix_applications_user_status on applications(user_id, status);
            create index if not exists ix_applications_user_updated on applications(user_id, updated_at desc);

            create table if not exists dream_companies (
                user_id uuid not null references users(id) on delete cascade,
                company_id text not null references companies(id) on delete cascade,
                created_at timestamptz not null default now(),
                primary key(user_id, company_id)
            );

            create table if not exists workspace_settings (
                id integer primary key check (id = 1),
                show_notifications boolean not null default false,
                show_matching boolean not null default false,
                updated_at timestamptz not null default now()
            );
            insert into workspace_settings (id) values (1) on conflict (id) do nothing;
            """;
        await using var command = new NpgsqlCommand(sql, connection);
        await command.ExecuteNonQueryAsync(ct);
    }

    public async Task<IReadOnlyList<ApplicationItem>> GetApplicationsAsync(Guid userId, CancellationToken ct = default)
    {
        await using var connection = new NpgsqlConnection(_connectionString);
        await connection.OpenAsync(ct);
        const string sql = """
            select a.id, a.job_id, j.company, j.title, j.location, j.application_url, a.status,
                   a.applied_at, a.resume_version, a.notes, a.follow_up_at, a.created_at, a.updated_at
            from applications a
            join jobs j on j.id = a.job_id
            where a.user_id = @user_id
            order by a.updated_at desc;
            """;
        await using var command = new NpgsqlCommand(sql, connection);
        command.Parameters.AddWithValue("user_id", userId);
        await using var reader = await command.ExecuteReaderAsync(ct);
        var result = new List<ApplicationItem>();
        while (await reader.ReadAsync(ct))
        {
            result.Add(new ApplicationItem(
                reader.GetGuid(0), reader.GetString(1), reader.GetString(2), reader.GetString(3),
                reader.IsDBNull(4) ? "" : reader.GetString(4), reader.GetString(5), reader.GetString(6),
                reader.IsDBNull(7) ? null : reader.GetFieldValue<DateTimeOffset>(7),
                reader.IsDBNull(8) ? null : reader.GetString(8), reader.IsDBNull(9) ? null : reader.GetString(9),
                reader.IsDBNull(10) ? null : reader.GetFieldValue<DateTimeOffset>(10),
                reader.GetFieldValue<DateTimeOffset>(11), reader.GetFieldValue<DateTimeOffset>(12)));
        }
        return result;
    }

    public async Task<ApplicationItem?> UpsertApplicationAsync(Guid userId, string jobId, ApplicationInput input, CancellationToken ct = default)
    {
        var status = string.IsNullOrWhiteSpace(input.Status) ? "NEW" : input.Status.Trim().ToUpperInvariant();
        var allowed = new[] { "NEW", "SHORTLISTED", "APPLIED", "INTERVIEW", "REJECTED", "CLOSED" };
        if (!allowed.Contains(status)) throw new ArgumentException("Invalid application status.");

        await using var connection = new NpgsqlConnection(_connectionString);
        await connection.OpenAsync(ct);
        const string sql = """
            insert into applications (id, user_id, job_id, status, applied_at, resume_version, notes, follow_up_at)
            values (@id, @user_id, @job_id, @status, @applied_at, @resume_version, @notes, @follow_up_at)
            on conflict (user_id, job_id) do update set
                status = excluded.status,
                applied_at = excluded.applied_at,
                resume_version = excluded.resume_version,
                notes = excluded.notes,
                follow_up_at = excluded.follow_up_at,
                updated_at = now()
            returning id;
            """;
        await using (var command = new NpgsqlCommand(sql, connection))
        {
            command.Parameters.AddWithValue("id", Guid.NewGuid());
            command.Parameters.AddWithValue("user_id", userId);
            command.Parameters.AddWithValue("job_id", jobId);
            command.Parameters.AddWithValue("status", status);
            command.Parameters.AddWithValue("applied_at", (object?)input.AppliedAt ?? DBNull.Value);
            command.Parameters.AddWithValue("resume_version", (object?)input.ResumeVersion ?? DBNull.Value);
            command.Parameters.AddWithValue("notes", (object?)input.Notes ?? DBNull.Value);
            command.Parameters.AddWithValue("follow_up_at", (object?)input.FollowUpAt ?? DBNull.Value);
            await command.ExecuteScalarAsync(ct);
        }

        return (await GetApplicationsAsync(userId, ct)).FirstOrDefault(item => item.JobId == jobId);
    }

    public async Task<IReadOnlyList<Company>> GetDreamCompaniesAsync(Guid userId, CancellationToken ct = default)
    {
        await using var connection = new NpgsqlConnection(_connectionString);
        await connection.OpenAsync(ct);
        const string sql = """
            select c.id, c.name, c.domain, c.initials, c.color, c.enabled, c.source_count, c.job_count, c.created_at
            from dream_companies d join companies c on c.id = d.company_id
            where d.user_id = @user_id order by c.name;
            """;
        await using var command = new NpgsqlCommand(sql, connection);
        command.Parameters.AddWithValue("user_id", userId);
        await using var reader = await command.ExecuteReaderAsync(ct);
        var result = new List<Company>();
        while (await reader.ReadAsync(ct))
            result.Add(new Company(reader.GetString(0), reader.GetString(1), reader.GetString(2), reader.GetString(3), reader.GetString(4), reader.GetBoolean(5), reader.GetInt32(6), reader.GetInt32(7), reader.GetFieldValue<DateTimeOffset>(8).ToString("O")));
        return result;
    }

    public async Task AddDreamCompanyAsync(Guid userId, string companyId, CancellationToken ct = default)
    {
        await using var connection = new NpgsqlConnection(_connectionString);
        await connection.OpenAsync(ct);
        const string sql = "insert into dream_companies (user_id, company_id) values (@user_id, @company_id) on conflict do nothing;";
        await using var command = new NpgsqlCommand(sql, connection);
        command.Parameters.AddWithValue("user_id", userId);
        command.Parameters.AddWithValue("company_id", companyId);
        await command.ExecuteNonQueryAsync(ct);
    }

    public async Task RemoveDreamCompanyAsync(Guid userId, string companyId, CancellationToken ct = default)
    {
        await using var connection = new NpgsqlConnection(_connectionString);
        await connection.OpenAsync(ct);
        const string sql = "delete from dream_companies where user_id = @user_id and company_id = @company_id;";
        await using var command = new NpgsqlCommand(sql, connection);
        command.Parameters.AddWithValue("user_id", userId);
        command.Parameters.AddWithValue("company_id", companyId);
        await command.ExecuteNonQueryAsync(ct);
    }

    public async Task<WorkspaceSettings> GetWorkspaceSettingsAsync(CancellationToken ct = default)
    {
        await using var connection = new NpgsqlConnection(_connectionString);
        await connection.OpenAsync(ct);
        const string sql = "select show_notifications, show_matching from workspace_settings where id = 1;";
        await using var command = new NpgsqlCommand(sql, connection);
        await using var reader = await command.ExecuteReaderAsync(ct);
        if (!await reader.ReadAsync(ct)) return new WorkspaceSettings(false, false);
        return new WorkspaceSettings(reader.GetBoolean(0), reader.GetBoolean(1));
    }

    public async Task SaveWorkspaceSettingsAsync(WorkspaceSettings input, CancellationToken ct = default)
    {
        await using var connection = new NpgsqlConnection(_connectionString);
        await connection.OpenAsync(ct);
        const string sql = "update workspace_settings set show_notifications = @show_notifications, show_matching = @show_matching, updated_at = now() where id = 1;";
        await using var command = new NpgsqlCommand(sql, connection);
        command.Parameters.AddWithValue("show_notifications", input.ShowNotifications);
        command.Parameters.AddWithValue("show_matching", input.ShowMatching);
        await command.ExecuteNonQueryAsync(ct);
    }
}

public record ApplicationInput(string Status, DateTimeOffset? AppliedAt, string? ResumeVersion, string? Notes, DateTimeOffset? FollowUpAt);
public record ApplicationItem(Guid Id, string JobId, string Company, string Title, string Location, string ApplicationUrl, string Status, DateTimeOffset? AppliedAt, string? ResumeVersion, string? Notes, DateTimeOffset? FollowUpAt, DateTimeOffset CreatedAt, DateTimeOffset UpdatedAt);
public record WorkspaceSettings(bool ShowNotifications, bool ShowMatching);
