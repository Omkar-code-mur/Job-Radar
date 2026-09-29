using Npgsql;

namespace JobRadar.Api;

public sealed record Feedback(
    Guid Id,
    Guid UserId,
    string Email,
    string Role,
    int Rating,
    string Liked,
    string Suggestions,
    DateTimeOffset CreatedAt);

public sealed record FeedbackInput(
    int Rating,
    string Liked,
    string Suggestions);

public sealed class FeedbackStore
{
    private readonly string _connectionString;

    public FeedbackStore(string connectionString) => _connectionString = connectionString;

    public async Task InitializeAsync(CancellationToken cancellationToken = default)
    {
        await using var connection = new NpgsqlConnection(_connectionString);
        await connection.OpenAsync(cancellationToken);

        const string sql = """
            create table if not exists feedback (
                id uuid primary key default gen_random_uuid(),
                user_id uuid not null references users(id) on delete cascade,
                rating integer not null check (rating between 1 and 5),
                liked text not null default '',
                suggestions text not null default '',
                created_at timestamptz not null default now()
            );

            create index if not exists ix_feedback_created_at on feedback(created_at desc);
            create index if not exists ix_feedback_user_id on feedback(user_id);
            """;

        await using var command = new NpgsqlCommand(sql, connection);
        await command.ExecuteNonQueryAsync(cancellationToken);
    }

    public async Task<Feedback> AddAsync(
        Guid userId,
        string email,
        string role,
        FeedbackInput input,
        CancellationToken cancellationToken = default)
    {
        var liked = (input.Liked ?? string.Empty).Trim();
        var suggestions = (input.Suggestions ?? string.Empty).Trim();

        if (input.Rating is < 1 or > 5)
            throw new ArgumentException("Rating must be between 1 and 5.");
        if (liked.Length > 2000)
            throw new ArgumentException("What you liked must be 2000 characters or fewer.");
        if (suggestions.Length > 4000)
            throw new ArgumentException("Suggestions must be 4000 characters or fewer.");
        if (string.IsNullOrWhiteSpace(liked) && string.IsNullOrWhiteSpace(suggestions))
            throw new ArgumentException("Please share what you liked or a suggestion.");

        const string sql = """
            insert into feedback (user_id, rating, liked, suggestions)
            values (@user_id, @rating, @liked, @suggestions)
            returning id, created_at;
            """;

        await using var connection = new NpgsqlConnection(_connectionString);
        await connection.OpenAsync(cancellationToken);
        await using var command = new NpgsqlCommand(sql, connection);
        command.Parameters.AddWithValue("user_id", userId);
        command.Parameters.AddWithValue("rating", input.Rating);
        command.Parameters.AddWithValue("liked", liked);
        command.Parameters.AddWithValue("suggestions", suggestions);

        await using var reader = await command.ExecuteReaderAsync(cancellationToken);
        await reader.ReadAsync(cancellationToken);

        return new Feedback(
            reader.GetGuid(0),
            userId,
            email,
            role,
            input.Rating,
            liked,
            suggestions,
            reader.GetFieldValue<DateTimeOffset>(1));
    }

    public async Task<IReadOnlyList<Feedback>> GetAsync(
        Guid userId,
        bool all,
        CancellationToken cancellationToken = default)
    {
        const string sql = """
            select f.id, f.user_id, u.email, u.role, f.rating, f.liked, f.suggestions, f.created_at
            from feedback f
            join users u on u.id = f.user_id
            where (@all or f.user_id = @user_id)
            order by f.created_at desc
            limit 200;
            """;

        await using var connection = new NpgsqlConnection(_connectionString);
        await connection.OpenAsync(cancellationToken);
        await using var command = new NpgsqlCommand(sql, connection);
        command.Parameters.AddWithValue("all", all);
        command.Parameters.AddWithValue("user_id", userId);
        await using var reader = await command.ExecuteReaderAsync(cancellationToken);

        var result = new List<Feedback>();
        while (await reader.ReadAsync(cancellationToken))
        {
            result.Add(new Feedback(
                reader.GetGuid(0),
                reader.GetGuid(1),
                reader.GetString(2),
                reader.GetString(3),
                reader.GetInt32(4),
                reader.GetString(5),
                reader.GetString(6),
                reader.GetFieldValue<DateTimeOffset>(7)));
        }

        return result;
    }
}
