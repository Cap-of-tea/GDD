using System.Text.Json;
using GDD.ViewModels;

namespace GDD.Mcp.Tools;

public static class ExecutionTools
{
    public static void Register(McpToolRegistry registry, MainViewModel mainVm)
    {
        registry.Register(
            new McpToolDefinition
            {
                Name = "gdd_execute_js",
                Description = "Execute arbitrary JavaScript in a browser window and return the result.",
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
                var player = mainVm.Players.FirstOrDefault(p => p.PlayerId == playerId);
                if (player?.WebView?.CoreWebView2 is null)
                    return McpResult.Error($"Player {playerId} not found or not initialized");

                var result = await player.WebView.CoreWebView2.ExecuteScriptAsync(script);
                return McpResult.Text(result);
            });
    }
}
