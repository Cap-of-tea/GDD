using System.Text.Json;
using GDD.Abstractions;
using GDD.Models;
using Serilog;

namespace GDD.Services;

public sealed class TokenInjectionService
{
    private static readonly ILogger Logger = Log.ForContext<TokenInjectionService>();

    public async Task InjectAsync(IBrowserEngine engine, AuthResult authResult, string frontendUrl)
    {
        var state = new NoiseAuthState
        {
            State = new NoiseAuthStateInner
            {
                AccessToken = authResult.AccessToken,
                SessionToken = authResult.SessionToken,
                User = authResult.User,
                Platform = "pwa",
                IsAuthenticated = true
            },
            Version = 0
        };

        var json = JsonSerializer.Serialize(state);
        var escapedJson = JsonSerializer.Serialize(json);

        var script = $"localStorage.setItem('noise-auth', {escapedJson});";
        await engine.ExecuteJavaScriptAsync(script);

        Logger.Information("Tokens injected for user {Username}", authResult.User?.Username);

        await engine.NavigateAsync(frontendUrl);
    }
}
