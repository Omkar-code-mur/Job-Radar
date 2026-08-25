using System.Text.Json.Serialization;

namespace JobRadar.Api.Sources.Greenhouse;

public sealed record GreenhouseResponse(
    [property: JsonPropertyName("jobs")] List<GreenhouseJob>? Jobs);

public sealed record GreenhouseJob(
    [property: JsonPropertyName("id")] long Id,
    [property: JsonPropertyName("title")] string? Title,
    [property: JsonPropertyName("content")] string? Content,
    [property: JsonPropertyName("absolute_url")] string? AbsoluteUrl,
    [property: JsonPropertyName("updated_at")] DateTimeOffset? UpdatedAt,
    [property: JsonPropertyName("location")] GreenhouseLocation? Location,
    [property: JsonPropertyName("departments")] List<GreenhouseNamedValue>? Departments,
    [property: JsonPropertyName("offices")] List<GreenhouseNamedValue>? Offices);

public sealed record GreenhouseLocation([property: JsonPropertyName("name")] string? Name);

public sealed record GreenhouseNamedValue([property: JsonPropertyName("name")] string? Name);