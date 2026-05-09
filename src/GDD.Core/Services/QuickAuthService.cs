using System.Net;
using System.Net.Http;
using System.Net.Http.Json;
using System.Text.Json;
using GDD.Models;
using Serilog;

namespace GDD.Services;

public sealed class QuickAuthService
{
    private static readonly ILogger Logger = Log.ForContext<QuickAuthService>();
    private readonly IHttpClientFactory _httpClientFactory;

    public QuickAuthService(IHttpClientFactory httpClientFactory)
    {
        _httpClientFactory = httpClientFactory;
    }

    public async Task<AuthResult?> RegisterAndLoginAsync(int playerId)
    {
        var email = $"player{playerId}@gdd.test";
        var password = $"BxN-Player{playerId}!";
        var username = $"bxn_player{playerId}";

        var client = _httpClientFactory.CreateClient("GDDAuth");

        var result = await TryRegisterAsync(client, email, password, username);
        if (result is not null) return result;

        result = await TryLoginAsync(client, email, password);
        return result;
    }

    private async Task<AuthResult?> TryRegisterAsync(
        HttpClient client, string email, string password, string username)
    {
        try
        {
            var response = await client.PostAsJsonAsync("auth/register", new
            {
                email,
                password,
                username,
                display_name = username,
                language = "en",
                timezone = "UTC"
            });

            if (response.StatusCode == HttpStatusCode.Conflict)
            {
                Logger.Debug("Player {Email} already exists, falling back to login", email);
                return null;
            }

            response.EnsureSuccessStatusCode();

            var envelope = await response.Content.ReadFromJsonAsync<ApiEnvelope<AuthResult>>();
            if (envelope?.Data is null)
                throw new InvalidOperationException("Empty auth response from register");

            Logger.Information("Registered {Email} → user {UserId}", email, envelope.Data.User?.Id);
            return envelope.Data;
        }
        catch (HttpRequestException ex) when (ex.StatusCode == HttpStatusCode.Conflict)
        {
            return null;
        }
        catch (HttpRequestException ex) when (ex.StatusCode == HttpStatusCode.TooManyRequests)
        {
            Logger.Warning("Rate limited on register. Set NOISE_RATE_LIMIT_REGISTER_PER_HOUR=1000");
            throw;
        }
    }

    private async Task<AuthResult?> TryLoginAsync(HttpClient client, string email, string password)
    {
        var response = await client.PostAsJsonAsync("auth/login", new { email, password });
        response.EnsureSuccessStatusCode();

        var envelope = await response.Content.ReadFromJsonAsync<ApiEnvelope<AuthResult>>();
        if (envelope?.Data is null)
            throw new InvalidOperationException("Empty auth response from login");

        Logger.Information("Logged in {Email} → user {UserId}", email, envelope.Data.User?.Id);
        return envelope.Data;
    }
}
