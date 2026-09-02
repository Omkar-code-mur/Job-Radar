using System.Security.Claims;
using Npgsql;

namespace JobRadar.Api.Auth;

public sealed record CurrentUser(
    Guid Id,
    string Email,
    string? DisplayName,
    string Role,
    DateTimeOffset CreatedAt);

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
                role text not null default 'USER' check (role in ('USER', 'ADMIN')),
                created_at timestamptz not null default now(),
                updated_at timestamptz not null default now()
            );
            """;

        await using var command = new NpgsqlCommand(sql, connection);
        await command.ExecuteNonQueryAsync(cancellationToken);
    }

    public async Task<CurrentUser?> GetOrCreateAsync(
        ClaimsPrincipal principal,
        string? adminEmail,
        CancellationToken cancellationToken = default)
    {
        // Accept both the Supabase JWT-style claims and the standard .NET claim types.
        // This keeps the identity lookup resilient to claim-type mapping performed by
        // authentication handlers or middleware.
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
            values (@id, @email, case when @admin_email is not null and lower(@email) = lower(@admin_email) then 'ADMIN' else 'USER' end)
            on conflict (id) do update set
                email = excluded.email,
                role = case
                    when @admin_email is not null and lower(excluded.email) = lower(@admin_email) then 'ADMIN'
                    else users.role
                end,
                updated_at = now();
            """;

        await using (var upsert = new NpgsqlCommand(upsertSql, connection))
        {
            upsert.Parameters.AddWithValue("id", userId);
            upsert.Parameters.AddWithValue("email", email.Trim());
            upsert.Parameters.AddWithValue("admin_email", (object?)adminEmail ?? DBNull.Value);
            await upsert.ExecuteNonQueryAsync(cancellationToken);
        }

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
