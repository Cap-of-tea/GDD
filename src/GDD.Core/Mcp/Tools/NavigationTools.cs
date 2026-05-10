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

        registry.Register(
            new McpToolDefinition
            {
                Name = "gdd_reload",
                Description = "Reload the current page. Use hard=true to bypass cache (like Ctrl+Shift+R).",
                InputSchema = new
                {
                    type = "object",
                    properties = new
                    {
                        player_id = new { type = "integer", description = "Player ID" },
                        hard = new { type = "boolean", description = "Hard reload — ignore cache (default false)" }
                    },
                    required = new[] { "player_id" }
                }
            },
            async args =>
            {
                var playerId = args?.GetProperty("player_id").GetInt32() ?? 0;
                var hard = args?.TryGetProperty("hard", out var hEl) == true && hEl.GetBoolean();
                var player = playerManager.GetPlayer(playerId);
                if (player?.Engine is null)
                    return McpResult.Error($"Player {playerId} not found or not initialized");

                await player.Engine.CallCdpMethodAsync("Page.reload",
                    JsonSerializer.Serialize(new { ignoreCache = hard }));
                await Task.Delay(500);
                var mode = hard ? "hard " : "";
                return McpResult.Text($"Page {mode}reloaded for player {playerId}");
            });

        registry.Register(
            new McpToolDefinition
            {
                Name = "gdd_back",
                Description = "Navigate back in browser history.",
                InputSchema = new
                {
                    type = "object",
                    properties = new
                    {
                        player_id = new { type = "integer", description = "Player ID" }
                    },
                    required = new[] { "player_id" }
                }
            },
            async args =>
            {
                var playerId = args?.GetProperty("player_id").GetInt32() ?? 0;
                var player = playerManager.GetPlayer(playerId);
                if (player?.Engine is null)
                    return McpResult.Error($"Player {playerId} not found or not initialized");

                var histJson = await player.Engine.CallCdpMethodWithResultAsync(
                    "Page.getNavigationHistory", "{}");
                using var doc = JsonDocument.Parse(histJson);
                var idx = doc.RootElement.GetProperty("currentIndex").GetInt32();
                if (idx <= 0)
                    return McpResult.Error($"Player {playerId}: no back history");

                var entries = doc.RootElement.GetProperty("entries");
                var entryId = entries[idx - 1].GetProperty("id").GetInt32();
                await player.Engine.CallCdpMethodAsync("Page.navigateToHistoryEntry",
                    JsonSerializer.Serialize(new { entryId }));
                await Task.Delay(500);
                return McpResult.Text($"Navigated back on player {playerId}");
            });

        registry.Register(
            new McpToolDefinition
            {
                Name = "gdd_forward",
                Description = "Navigate forward in browser history.",
                InputSchema = new
                {
                    type = "object",
                    properties = new
                    {
                        player_id = new { type = "integer", description = "Player ID" }
                    },
                    required = new[] { "player_id" }
                }
            },
            async args =>
            {
                var playerId = args?.GetProperty("player_id").GetInt32() ?? 0;
                var player = playerManager.GetPlayer(playerId);
                if (player?.Engine is null)
                    return McpResult.Error($"Player {playerId} not found or not initialized");

                var histJson = await player.Engine.CallCdpMethodWithResultAsync(
                    "Page.getNavigationHistory", "{}");
                using var doc = JsonDocument.Parse(histJson);
                var idx = doc.RootElement.GetProperty("currentIndex").GetInt32();
                var entries = doc.RootElement.GetProperty("entries");
                if (idx >= entries.GetArrayLength() - 1)
                    return McpResult.Error($"Player {playerId}: no forward history");

                var entryId = entries[idx + 1].GetProperty("id").GetInt32();
                await player.Engine.CallCdpMethodAsync("Page.navigateToHistoryEntry",
                    JsonSerializer.Serialize(new { entryId }));
                await Task.Delay(500);
                return McpResult.Text($"Navigated forward on player {playerId}");
            });
    }
}
