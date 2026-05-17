using System.Text.Json;
using GDD.Abstractions;

namespace GDD.Mcp.Tools;

public static class ExecutionTools
{
    public static void Register(McpToolRegistry registry, IPlayerManager playerManager)
    {
        registry.Register(
            new McpToolDefinition
            {
                Name = "gdd_execute_js",
                Description = "Execute arbitrary JavaScript code in a browser player's page context and return the result as a string. The script runs in the page's global scope with full access to document, window, and all page APIs. Use for DOM manipulation, reading complex state, or actions not covered by other tools.",
                InputSchema = new
                {
                    type = "object",
                    properties = new
                    {
                        player_id = new { type = "integer", description = "Player ID" },
                        script = new { type = "string", description = "JavaScript code to execute" }
                    },
                    required = new[] { "player_id", "script" }
                }
            },
            async args =>
            {
                var playerId = args?.GetProperty("player_id").GetInt32() ?? 0;
                var script = args?.GetProperty("script").GetString() ?? "";
                var player = playerManager.GetPlayer(playerId);
                if (player?.Engine is null)
                    return McpResult.Error($"Player {playerId} not found or not initialized");

                var result = await player.Engine.ExecuteJavaScriptAsync(script);
                return McpResult.Text(result);
            });
    }
}
