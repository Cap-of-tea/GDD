using System.Text.Json;
using GDD.Abstractions;

namespace GDD.Mcp.Tools;

public static class NavigationTools
{
    public static void Register(McpToolRegistry registry, IPlayerManager playerManager)
    {
        registry.Register(
            new McpToolDefinition
            {
                Name = "gdd_navigate",
                Description = "Navigate a browser window to a URL.",
                InputSchema = new
                {
                    type = "object",
                    properties = new
                    {
                        player_id = new { type = "integer", description = "Player ID" },
                        url = new { type = "string", description = "URL to navigate to" }
                    },
                    required = new[] { "player_id", "url" }
                }
            },
            async args =>
            {
                var playerId = args?.GetProperty("player_id").GetInt32() ?? 0;
                var url = args?.GetProperty("url").GetString() ?? "";
                var player = playerManager.GetPlayer(playerId);
                if (player?.Engine is null)
                    return McpResult.Error($"Player {playerId} not found or not initialized");
                await player.Engine.NavigateAsync(url);
                await Task.Delay(500);
                return McpResult.Text($"Navigated player {playerId} to {url}");
            });

        registry.Register(
            new McpToolDefinition
            {
                Name = "gdd_wait",
                Description = "Wait for a CSS selector to appear in the page. Returns success or timeout error.",
                InputSchema = new
                {
                    type = "object",
                    properties = new
                    {
                        player_id = new { type = "integer", description = "Player ID" },
                        selector = new { type = "string", description = "CSS selector to wait for" },
                        timeout = new { type = "integer", description = "Timeout in ms (default 5000)", @default = 5000 }
                    },
                    required = new[] { "player_id", "selector" }
                }
            },
            async args =>
            {
                var playerId = args?.GetProperty("player_id").GetInt32() ?? 0;
                var selector = args?.GetProperty("selector").GetString() ?? "";
                var timeout = 5000;
                if (args?.TryGetProperty("timeout", out var timeoutEl) == true)
                    timeout = timeoutEl.GetInt32();

                var player = playerManager.GetPlayer(playerId);
                if (player?.Engine is null)
                    return McpResult.Error($"Player {playerId} not found or not initialized");

                var escapedSelector = selector.Replace("'", "\\'");
                var elapsed = 0;

                while (elapsed < timeout)
                {
                    var result = await player.Engine.ExecuteJavaScriptAsync(
                        $"document.querySelector('{escapedSelector}') !== null");
                    if (result == "true")
                        return McpResult.Text($"Found '{selector}' after {elapsed}ms");
                    await Task.Delay(200);
                    elapsed += 200;
                }

                return McpResult.Error($"Timeout: '{selector}' not found after {timeout}ms");
            });
    }
}
