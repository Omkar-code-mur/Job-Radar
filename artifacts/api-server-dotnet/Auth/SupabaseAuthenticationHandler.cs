using System.Net.Http.Headers;
using System.Security.Claims;
using System.Text.Json;
using Microsoft.AspNetCore.Authentication;
using Microsoft.Extensions.Options;

namespace JobRadar.Api.Auth;

public sealed class SupabaseAuthenticationHandler : AuthenticationHandler<AuthenticationSchemeOptions>
{
    private readonly IHttpClientFactory _httpClientFactory;
    private readonly string _supabaseUrl;
    private readonly string _supabaseKey;

    public SupabaseAuthenticationHandler(
        IOptionsMonitor<AuthenticationSchemeOptions> options,
        ILoggerFactory logger,
        System.Text.Encodings.Web.UrlEncoder encoder,
        IHttpClientFactory httpClientFactory,
        IConfiguration configuration)
        : base(options, logger, encoder)
    {
        _httpClientFactory = httpClientFactory;
        _supabaseUrl = (configuration["SUPABASE_URL"] ?? string.Empty).TrimEnd('/');
        _supabaseKey = configuration["SUPABASE_ANON_KEY"]
            ?? configuration["SUPABASE_PUBLISHABLE_KEY"]
            ?? string.Empty;
    }

    protected override async Task<AuthenticateResult> HandleAuthenticateAsync()
    {
        if (!Request.Headers.TryGetValue("Authorization", out var authorization))
            return AuthenticateResult.NoResult();

        if (!AuthenticationHeaderValue.TryParse(authorization.ToString(), out var header)
            || !string.Equals(header.Scheme, "Bearer", StringComparison.OrdinalIgnoreCase)
            || string.IsNullOrWhiteSpace(header.Parameter))
        {
            return AuthenticateResult.Fail("Invalid Authorization header.");
        }

        if (string.IsNullOrWhiteSpace(_supabaseUrl) || string.IsNullOrWhiteSpace(_supabaseKey))
            return AuthenticateResult.Fail("Supabase authentication is not configured.");

        try
        {
            var client = _httpClientFactory.CreateClient("SupabaseAuth");
            using var request = new HttpRequestMessage(HttpMethod.Get, $"{_supabaseUrl}/auth/v1/user");
            request.Headers.TryAddWithoutValidation("apikey", _supabaseKey);
            request.Headers.Authorization = new AuthenticationHeaderValue("Bearer", header.Parameter);

            using var response = await client.SendAsync(request, Context.RequestAborted);
            if (!response.IsSuccessStatusCode)
                return AuthenticateResult.Fail("Supabase access token is invalid or expired.");

            await using var stream = await response.Content.ReadAsStreamAsync(Context.RequestAborted);
            var user = await JsonSerializer.DeserializeAsync<SupabaseUser>(
                stream,
                new JsonSerializerOptions { PropertyNameCaseInsensitive = true },
                Context.RequestAborted);

            if (user is null || !Guid.TryParse(user.Id, out var userId) || string.IsNullOrWhiteSpace(user.Email))
                return AuthenticateResult.Fail("Supabase user response is missing required identity claims.");

            var claims = new List<Claim>
            {
                new(ClaimTypes.NameIdentifier, userId.ToString()),
                new(ClaimTypes.Email, user.Email),
                new("sub", userId.ToString()),
            };

            if (!string.IsNullOrWhiteSpace(user.Role))
                claims.Add(new Claim("role", user.Role));

            var identity = new ClaimsIdentity(claims, Scheme.Name);
            var principal = new ClaimsPrincipal(identity);
            return AuthenticateResult.Success(new AuthenticationTicket(principal, Scheme.Name));
        }
        catch (OperationCanceledException) when (Context.RequestAborted.IsCancellationRequested)
        {
            throw;
        }
        catch (Exception exception)
        {
            Logger.LogError(exception, "Supabase authentication request failed.");
            return AuthenticateResult.Fail("Supabase authentication could not be verified.");
        }
    }

    private sealed record SupabaseUser(string Id, string Email, string? Role);
}
