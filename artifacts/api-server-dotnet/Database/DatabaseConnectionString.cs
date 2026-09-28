using Npgsql;

namespace JobRadar.Api.Database;

public static class DatabaseConnectionString
{
    public static string Normalize(string connectionString)
    {
        var value = connectionString.Trim();

        if (!value.StartsWith("postgres://", StringComparison.OrdinalIgnoreCase) &&
            !value.StartsWith("postgresql://", StringComparison.OrdinalIgnoreCase))
        {
            return value;
        }

        if (!Uri.TryCreate(value, UriKind.Absolute, out var uri))
            throw new InvalidOperationException("DATABASE_URL is not a valid PostgreSQL connection URI.");

        var builder = new NpgsqlConnectionStringBuilder
        {
            Host = uri.Host,
            Port = uri.Port > 0 ? uri.Port : 5432,
            Database = uri.AbsolutePath.Trim('/'),
            Username = Uri.UnescapeDataString(uri.UserInfo.Split(':', 2)[0]),
            Password = uri.UserInfo.Contains(':')
                ? Uri.UnescapeDataString(uri.UserInfo[(uri.UserInfo.IndexOf(':') + 1)..])
                : string.Empty,
            SslMode = SslMode.Require
        };

        return builder.ConnectionString;
    }
}
