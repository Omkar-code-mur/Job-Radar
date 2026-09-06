using System.Text.Json;
using Npgsql;

public sealed class ProfileImportService
{
    private readonly string _connectionString;
    public ProfileImportService(string connectionString) => _connectionString = connectionString;

    public async Task InitializeAsync(CancellationToken ct = default)
    {
        await using var connection = new NpgsqlConnection(_connectionString);
        await connection.OpenAsync(ct);
        await using var command = new NpgsqlCommand("create table if not exists profile_details (user_id uuid primary key references users(id) on delete cascade, profile_json jsonb not null, imported_at timestamptz not null);", connection);
        await command.ExecuteNonQueryAsync(ct);
    }

    public static string Prompt => """
You are helping me create my profile for Job Radar, an AI-assisted job hunting tool for lazy-but-smart job seekers. Job Radar will use this profile to find relevant jobs, rank them, reduce repetitive job-search work, and help me focus on learning and interview preparation.

Using ONLY information you already know about me from our previous conversations, chat history, saved memory, uploaded information, or context available to you:

1. Build my Job Radar professional and job-search profile using the exact JSON format below.
2. Be completely honest. Do NOT guess, infer, exaggerate, or invent information.
3. If you do not know something, use null for a single unknown value and [] for an unknown list.
4. If information may be outdated or uncertain, put it in notes rather than presenting it as confirmed.
5. Preserve my actual experience level. Never upgrade my seniority or claim I have used a technology unless you have evidence from our conversations.
6. Distinguish demonstrated/used skills from skills I have only discussed, studied, or expressed interest in.
7. Include projects only when you know I actually worked on or built them.
8. Include job-search preferences only when you actually know them.
9. Do not include passwords, secrets, financial account information, or other unnecessary sensitive personal information.
10. The JSON will be pasted directly into Job Radar. Return ONLY valid JSON. No markdown fences, explanation, or text before or after it.

Use this exact format:

{
  "profileVersion": "1.0",
  "candidate": { "name": null, "currentTitle": null, "professionalSummary": null, "yearsOfExperience": null, "currentEmploymentStatus": null, "availability": null, "noticePeriod": null },
  "location": { "currentCity": null, "preferredCities": [], "country": null, "remotePreference": null, "relocationPreference": null },
  "target": { "targetRoles": [], "targetIndustries": [], "targetCompanies": [], "careerDirection": null, "minimumExperienceYears": null, "maximumExperienceYears": null },
  "skills": { "primary": [], "secondary": [], "programmingLanguages": [], "frameworks": [], "databases": [], "cloud": [], "aiAndMl": [], "devops": [], "tools": [], "other": [] },
  "experience": [],
  "projects": [],
  "education": [],
  "jobPreferences": { "employmentTypes": [], "workModes": [], "preferredIndustries": [], "avoidIndustries": [], "salaryExpectation": { "currency": null, "minimum": null, "maximum": null }, "willingToRelocate": null, "willingToWorkInternationally": null },
  "jobSearch": { "activelyLooking": null, "priorityRoles": [], "prioritySkills": [], "companiesOfInterest": [], "jobBoardsUsed": [], "applicationPreferences": null },
  "careerGoals": { "shortTerm": [], "longTerm": [], "skillsToDevelop": [], "rolesToTransitionInto": [] },
  "evidence": { "strongestSkills": [], "strongestProjects": [], "notableAchievements": [] },
  "unknowns": [],
  "notes": []
}
""";

    public async Task<JsonElement?> GetDetailsAsync(Guid userId, CancellationToken ct = default)
    {
        await using var connection = new NpgsqlConnection(_connectionString);
        await connection.OpenAsync(ct);
        await using var command = new NpgsqlCommand("select profile_json from profile_details where user_id=@user_id", connection);
        command.Parameters.AddWithValue("user_id", userId);
        var value = await command.ExecuteScalarAsync(ct);
        return value is null ? null : JsonDocument.Parse((string)value).RootElement.Clone();
    }

    public async Task SaveDetailsAsync(Guid userId, JsonElement profile, CancellationToken ct = default)
    {
        await using var connection = new NpgsqlConnection(_connectionString);
        await connection.OpenAsync(ct);
        await using var command = new NpgsqlCommand("insert into profile_details (user_id,profile_json,imported_at) values (@user_id,@profile,@imported_at) on conflict (user_id) do update set profile_json=excluded.profile_json, imported_at=excluded.imported_at", connection);
        command.Parameters.AddWithValue("user_id", userId);
        command.Parameters.AddWithValue("profile", NpgsqlTypes.NpgsqlDbType.Jsonb, profile.GetRawText());
        command.Parameters.AddWithValue("imported_at", DateTimeOffset.UtcNow);
        await command.ExecuteNonQueryAsync(ct);
    }

    public static ProfileInput ToProfileInput(JsonElement root, Profile existing)
    {
        var roles = Array(root, "target", "targetRoles");
        var skills = Merge(root, ("skills", "primary"), ("skills", "secondary"));
        var technologies = Merge(root, ("skills", "programmingLanguages"), ("skills", "frameworks"), ("skills", "databases"), ("skills", "cloud"), ("skills", "aiAndMl"), ("skills", "devops"), ("skills", "tools"), ("skills", "other"));
        var locations = Array(root, "location", "preferredCities");
        var workModes = Array(root, "jobPreferences", "workModes");
        var workplace = workModes.FirstOrDefault() ?? existing.WorkplacePreference;
        if (string.Equals(workplace, "onsite", StringComparison.OrdinalIgnoreCase) || string.Equals(workplace, "on-site", StringComparison.OrdinalIgnoreCase)) workplace = "On-site";
        if (!new[] { "Remote", "Hybrid", "On-site", "Any" }.Contains(workplace, StringComparer.OrdinalIgnoreCase)) workplace = existing.WorkplacePreference;
        var include = Merge(root, ("jobSearch", "prioritySkills"), ("jobSearch", "priorityRoles"));
        var exclude = Array(root, "jobPreferences", "avoidIndustries");
        var minYears = Int(root, "target", "minimumExperienceYears") ?? existing.MinYears;
        var maxYears = Int(root, "target", "maximumExperienceYears") ?? existing.MaxYears;
        return new ProfileInput(roles.Length > 0 ? roles : existing.Roles, skills.Length > 0 ? skills : existing.Skills, technologies.Length > 0 ? technologies : existing.Technologies, Math.Max(0, minYears), Math.Max(0, maxYears), locations.Length > 0 ? locations : existing.Locations, workplace, include.Length > 0 ? include : existing.IncludeKeywords, exclude.Length > 0 ? exclude : existing.ExcludeKeywords, existing.Email);
    }

    private static string[] Merge(JsonElement root, params (string parent, string child)[] fields) => fields.SelectMany(f => Array(root, f.parent, f.child)).Distinct(StringComparer.OrdinalIgnoreCase).ToArray();
    private static string[] Array(JsonElement root, params string[] path)
    {
        var node = root;
        foreach (var part in path) { if (node.ValueKind != JsonValueKind.Object || !node.TryGetProperty(part, out node)) return []; }
        return node.ValueKind == JsonValueKind.Array ? node.EnumerateArray().Where(x => x.ValueKind == JsonValueKind.String).Select(x => x.GetString()!).Where(x => !string.IsNullOrWhiteSpace(x)).Select(x => x.Trim()).ToArray() : [];
    }
    private static int? Int(JsonElement root, params string[] path)
    {
        var node = root;
        foreach (var part in path) { if (node.ValueKind != JsonValueKind.Object || !node.TryGetProperty(part, out node)) return null; }
        return node.ValueKind == JsonValueKind.Number && node.TryGetInt32(out var value) ? value : null;
    }
}
