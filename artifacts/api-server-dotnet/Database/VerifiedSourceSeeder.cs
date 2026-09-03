using Npgsql;

/// <summary>
/// Idempotently registers public job sources that have been manually verified against the
/// employer's careers presence. This is deliberately a small allow-list: a career page alone
/// is not treated as a monitorable source.
/// </summary>
public static class VerifiedSourceSeeder
{
    private sealed record Definition(
        string CompanyName,
        string Domain,
        string Initials,
        string Color,
        string SourceId,
        string SourceName,
        string Type,
        string Url,
        string? BoardToken);

    private static readonly Definition[] Sources =
    [
        new(
            "Deloitte",
            "deloitte.com",
            "DE",
            "#86BC25",
            "verified-deloitte-usi",
            "Deloitte USI Careers",
            "DELOITTE_USI",
            "https://usijobs.deloitte.com/careersUSI/SearchJobs",
            null),
        new(
            "NICE",
            "nice.com",
            "NI",
            "#00A9CE",
            "verified-nice-greenhouse",
            "NICE Greenhouse Careers",
            "GREENHOUSE_API",
            "https://job-boards.greenhouse.io/nice",
            "nice"),
        new(
            "Addepar",
            "addepar.com",
            "AD",
            "#111827",
            "verified-addepar-greenhouse",
            "Addepar Greenhouse Careers",
            "GREENHOUSE_API",
            "https://job-boards.greenhouse.io/addepar1",
            "addepar1")
    ];

    public static async Task SeedAsync(string connectionString, CancellationToken cancellationToken = default)
    {
        await using var connection = new NpgsqlConnection(connectionString);
        await connection.OpenAsync(cancellationToken);

        foreach (var definition in Sources)
        {
            var companyId = await EnsureCompanyAsync(connection, definition, cancellationToken);
            await EnsureSourceAsync(connection, companyId, definition, cancellationToken);
        }
    }

    private static async Task<string> EnsureCompanyAsync(
        NpgsqlConnection connection,
        Definition definition,
        CancellationToken cancellationToken)
    {
        await using (var lookup = new NpgsqlCommand(
            "select id from companies where lower(domain) = lower(@domain) order by created_at limit 1",
            connection))
        {
            lookup.Parameters.AddWithValue("domain", definition.Domain);
            var existing = await lookup.ExecuteScalarAsync(cancellationToken);
            if (existing is string id)
                return id;
        }

        var companyId = $"verified-{definition.Domain.Replace('.', '-') }";
        await using var insert = new NpgsqlCommand("""
            insert into companies (id, name, domain, initials, color, enabled, created_at)
            values (@id, @name, @domain, @initials, @color, true, now())
            on conflict (id) do nothing;
            """, connection);
        insert.Parameters.AddWithValue("id", companyId);
        insert.Parameters.AddWithValue("name", definition.CompanyName);
        insert.Parameters.AddWithValue("domain", definition.Domain);
        insert.Parameters.AddWithValue("initials", definition.Initials);
        insert.Parameters.AddWithValue("color", definition.Color);
        await insert.ExecuteNonQueryAsync(cancellationToken);
        return companyId;
    }

    private static async Task EnsureSourceAsync(
        NpgsqlConnection connection,
        string companyId,
        Definition definition,
        CancellationToken cancellationToken)
    {
        await using var command = new NpgsqlCommand("""
            insert into sources (
                id, company_id, name, type, url, board_token, enabled, status,
                last_fetch, jobs_fetched, failure_count, last_error)
            values (
                @id, @company_id, @name, @type, @url, @board_token, true, 'healthy',
                'Never', 0, 0, null)
            on conflict (id) do update set
                company_id = excluded.company_id,
                name = excluded.name,
                type = excluded.type,
                url = excluded.url,
                board_token = excluded.board_token,
                enabled = true;
            """, connection);
        command.Parameters.AddWithValue("id", definition.SourceId);
        command.Parameters.AddWithValue("company_id", companyId);
        command.Parameters.AddWithValue("name", definition.SourceName);
        command.Parameters.AddWithValue("type", definition.Type);
        command.Parameters.AddWithValue("url", definition.Url);
        command.Parameters.AddWithValue("board_token", (object?)definition.BoardToken ?? DBNull.Value);
        await command.ExecuteNonQueryAsync(cancellationToken);
    }
}
