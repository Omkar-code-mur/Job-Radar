using System.Security.Claims;
using Npgsql;

namespace JobRadar.Api.Auth;

public sealed record CurrentUser(
    Guid Id,
    string Email,
    string? DisplayName,
    string Role,
    DateTimeOffset CreatedAt);

public sealed record ManagedUser(
    Guid Id,
    string Email,
    string? DisplayName,
    string Role,
    DateTimeOffset CreatedAt,
    DateTimeOffset UpdatedAt);

public sealed class UserIdentityStore
{
    private readonly string _connectionString;

    public UserIdentityStore(string connectionString)
    {
        _connectionString = connectionString;
    }

    public async Task InitializeAsync(CancellationToken cancellationToken = default)
    {
        await using var connection = new NpgsqlConnection(_connectionString);
        await connection.OpenAsync(cancellationToken);

        const string sql = """
            create table if not exists users (
                id uuid primary key,
                email text not null unique,
                display_name text null,
                role text not null default 'USER',
                created_at timestamptz not null default now(),
                updated_at timestamptz not null default now()
            );

            alter table users drop constraint if exists users_role_check;

            alter table users
                add constraint users_role_check
                check (role in ('USER', 'ADMIN', 'SUPER_ADMIN'));
            """;

        await using var command = new NpgsqlCommand(sql, connection);
        await command.ExecuteNonQueryAsync(cancellationToken);
    }

    public async Task<CurrentUser?> GetOrCreateAsync(
        ClaimsPrincipal principal,
        string? adminEmail,
        string? superAdminEmail,
        CancellationToken cancellationToken = default)
    {
        var subject = principal.FindFirst("sub")?.Value
            ?? principal.FindFirst(ClaimTypes.NameIdentifier)?.Value;
        var email = principal.FindFirst("email")?.Value
            ?? principal.FindFirst(ClaimTypes.Email)?.Value;

        if (!Guid.TryParse(subject, out var userId) || string.IsNullOrWhiteSpace(email))
            return null;

        await using var connection = new NpgsqlConnection(_connectionString);
        await connection.OpenAsync(cancellationToken);

        const string upsertSql = """
            insert into users (id, email, role)
            values (
                @id,
                @email,
                case
                    when @super_admin_email is not null and lower(@email) = lower(@super_admin_email) then 'SUPER_ADMIN'
                    when @admin_email is not null and lower(@email) = lower(@admin_email) then 'ADMIN'
                    else 'USER'
                end
            )
            on conflict (id) do update set
                email = excluded.email,
                role = case
                    when @super_admin_email is not null and lower(excluded.email) = lower(@super_admin_email) then 'SUPER_ADMIN'
                    when @admin_email is not null and lower(excluded.email) = lower(@admin_email) and users.role = 'USER' then 'ADMIN'
                    else users.role
                end,
                updated_at = now();
            """;

        await using (var upsert = new NpgsqlCommand(upsertSql, connection))
        {
            upsert.Parameters.AddWithValue("id", userId);
            upsert.Parameters.AddWithValue("email", email.Trim());
            upsert.Parameters.AddWithValue("admin_email", (object?)adminEmail ?? DBNull.Value);
            upsert.Parameters.AddWithValue("super_admin_email", (object?)superAdminEmail ?? DBNull.Value);
            await upsert.ExecuteNonQueryAsync(cancellationToken);
        }

        return await GetByIdAsync(userId, connection, cancellationToken);
    }

    public async Task<IReadOnlyList<ManagedUser>> GetUsersAsync(CancellationToken cancellationToken = default)
    {
        await using var connection = new NpgsqlConnection(_connectionString);
        await connection.OpenAsync(cancellationToken);

        const string sql = """
            select id, email, display_name, role, created_at, updated_at
            from users
            order by created_at desc;
            """;

        await using var command = new NpgsqlCommand(sql, connection);
        await using var reader = await command.ExecuteReaderAsync(cancellationToken);

        var users = new List<ManagedUser>();
        while (await reader.ReadAsync(cancellationToken))
        {
            users.Add(new ManagedUser(
                reader.GetGuid(0),
                reader.GetString(1),
                reader.IsDBNull(2) ? null : reader.GetString(2),
                reader.GetString(3),
                reader.GetFieldValue<DateTimeOffset>(4),
                reader.GetFieldValue<DateTimeOffset>(5)));
        }

        return users;
    }

    public async Task<ManagedUser?> UpdateAdminAccessAsync(
        Guid targetUserId,
        bool makeAdmin,
        Guid actorUserId,
        CancellationToken cancellationToken = default)
    {
        if (targetUserId == actorUserId)
            return null;

        await using var connection = new NpgsqlConnection(_connectionString);
        await connection.OpenAsync(cancellationToken);

        const string sql = """
            update users
            set role = case when @make_admin then 'ADMIN' else 'USER' end,
                updated_at = now()
            where id = @id
              and role <> 'SUPER_ADMIN'
            returning id, email, display_name, role, created_at, updated_at;
            """;

        await using var command = new NpgsqlCommand(sql, connection);
        command.Parameters.AddWithValue("id", targetUserId);
        command.Parameters.AddWithValue("make_admin", makeAdmin);

        await using var reader = await command.ExecuteReaderAsync(cancellationToken);
        if (!await reader.ReadAsync(cancellationToken))
            return null;

        return new ManagedUser(
            reader.GetGuid(0),
            reader.GetString(1),
            reader.IsDBNull(2) ? null : reader.GetString(2),
            reader.GetString(3),
            reader.GetFieldValue<DateTimeOffset>(4),
            reader.GetFieldValue<DateTimeOffset>(5));
    }

    private static async Task<CurrentUser?> GetByIdAsync(
        Guid userId,
        NpgsqlConnection connection,
        CancellationToken cancellationToken)
    {
        const string selectSql = """
            select id, email, display_name, role, created_at
            from users
            where id = @id;
            """;

        await using var select = new NpgsqlCommand(selectSql, connection);
        select.Parameters.AddWithValue("id", userId);
        await using var reader = await select.ExecuteReaderAsync(cancellationToken);

        if (!await reader.ReadAsync(cancellationToken))
            return null;

        return new CurrentUser(
            reader.GetGuid(0),
            reader.GetString(1),
            reader.IsDBNull(2) ? null : reader.GetString(2),
            reader.GetString(3),
            reader.GetFieldValue<DateTimeOffset>(4));
    }
}
