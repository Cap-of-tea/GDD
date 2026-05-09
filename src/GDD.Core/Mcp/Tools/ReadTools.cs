using System.Text.Json;
using GDD.Abstractions;

namespace GDD.Mcp.Tools;

public static class ReadTools
{
    public static void Register(McpToolRegistry registry, IPlayerManager playerManager)
    {
        registry.Register(
            new McpToolDefinition
            {
                Name = "gdd_read",
                Description = "Read text content of an element by CSS selector.",
                InputSchema = new
                {
                    type = "object",
                    properties = new
                    {
                        player_id = new { type = "integer", description = "Player ID" },
                        selector = new { type = "string", description = "CSS selector" }
                    },
                    required = new[] { "player_id", "selector" }
                }
            },
            async args =>
            {
                var playerId = args?.GetProperty("player_id").GetInt32() ?? 0;
                var selector = args?.GetProperty("selector").GetString() ?? "";
                var player = playerManager.GetPlayer(playerId);
                if (player?.Engine is null)
                    return McpResult.Error($"Player {playerId} not found or not initialized");

                var escaped = selector.Replace("'", "\\'");
                var result = await player.Engine.ExecuteJavaScriptAsync(
                    $"document.querySelector('{escaped}')?.textContent ?? null");

                if (result == "null")
                    return McpResult.Error($"Element '{selector}' not found");

                var text = JsonSerializer.Deserialize<string>(result);
                return McpResult.Text(text ?? "");
            });

        registry.Register(
            new McpToolDefinition
            {
                Name = "gdd_read_all",
                Description = "Read text content of all elements matching a CSS selector. Returns JSON array.",
                InputSchema = new
                {
                    type = "object",
                    properties = new
                    {
                        player_id = new { type = "integer", description = "Player ID" },
                        selector = new { type = "string", description = "CSS selector" }
                    },
                    required = new[] { "player_id", "selector" }
                }
            },
            async args =>
            {
                var playerId = args?.GetProperty("player_id").GetInt32() ?? 0;
                var selector = args?.GetProperty("selector").GetString() ?? "";
                var player = playerManager.GetPlayer(playerId);
                if (player?.Engine is null)
                    return McpResult.Error($"Player {playerId} not found or not initialized");

                var escaped = selector.Replace("'", "\\'");
                var result = await player.Engine.ExecuteJavaScriptAsync(
                    $"JSON.stringify([...document.querySelectorAll('{escaped}')].map(el => el.textContent))");

                var text = JsonSerializer.Deserialize<string>(result);
                return McpResult.Text(text ?? "[]");
            });

        registry.Register(
            new McpToolDefinition
            {
                Name = "gdd_screenshot",
                Description = "Take a screenshot of a browser window. Returns base64 PNG image.",
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

                var screenshotBytes = await player.Engine.CaptureScreenshotAsync();
                var base64 = Convert.ToBase64String(screenshotBytes);

                return new McpToolResult
                {
                    Content = new List<McpContent>
                    {
                        new()
                        {
                            Type = "image",
                            Data = base64,
                            MimeType = "image/png"
                        }
                    }
                };
            });
    }
}
