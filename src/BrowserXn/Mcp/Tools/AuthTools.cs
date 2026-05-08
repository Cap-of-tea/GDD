using System.Text.Json;
using GDD.Models;
using GDD.Services;
using GDD.ViewModels;

namespace GDD.Mcp.Tools;

public static class AuthTools
{
    public static void Register(
        McpToolRegistry registry,
        MainViewModel mainVm,
        QuickAuthService authService,
        TokenInjectionService tokenService,
        AppConfig config)
    {
        registry.Register(
            new McpToolDefinition
            {
                Name = "gdd_quick_auth",
                Description = "Register and login a player with auto-generated credentials, injecting tokens into the browser.",
                InputSchema = new
                {
                    type = "object",
                    properties = new
                    {
                        player_id = new { type = "integer", description = "Player ID (or 0 for all players)" }
                    },
                    required = new[] { "player_id" }
                }
            },
            async args =>
            {
                var playerId = args?.GetProperty("player_id").GetInt32() ?? 0;
                var targets = playerId == 0
                    ? mainVm.Players.ToList()
                    : mainVm.Players.Where(p => p.PlayerId == playerId).ToList();

                if (targets.Count == 0)
                    return McpResult.Error(playerId == 0
                        ? "No players to authenticate"
                        : $"Player {playerId} not found");

                var results = new List<string>();

                foreach (var batch in targets.Chunk(8))
                {
                    var tasks = batch.Select(async player =>
                    {
                        try
                        {
                            if (player.WebView?.CoreWebView2 is null)
                                return $"Player {player.PlayerId}: not initialized";

                            var authResult = await authService.RegisterAndLoginAsync(player.PlayerId);
                            if (authResult is null)
                                return $"Player {player.PlayerId}: auth failed";

                            await System.Windows.Application.Current.Dispatcher.InvokeAsync(async () =>
                            {
                                await tokenService.InjectAsync(
                                    player.WebView.CoreWebView2, authResult, config.FrontendUrl);
                            }).Task;

                            return $"Player {player.PlayerId}: authenticated as {authResult.User?.Username}";
                        }
                        catch (Exception ex)
                        {
                            return $"Player {player.PlayerId}: error - {ex.Message}";
                        }
                    });

                    results.AddRange(await Task.WhenAll(tasks));
                    if (batch.Length == 8)
                        await Task.Delay(500);
                }

                return McpResult.Text(string.Join("\n", results));
            });
    }
}
