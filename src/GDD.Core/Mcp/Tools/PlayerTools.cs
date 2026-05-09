using System.Text.Json;
using GDD.Abstractions;

namespace GDD.Mcp.Tools;

public static class PlayerTools
{
    public static void Register(McpToolRegistry registry, IPlayerManager playerManager)
    {
        registry.Register(
            new McpToolDefinition
            {
                Name = "gdd_add_players",
                Description = "Add N new browser instances. Returns list of created player IDs.",
                InputSchema = new
                {
                    type = "object",
                    properties = new
                    {
                        count = new { type = "integer", minimum = 1, maximum = 64, description = "Number of players to add" }
                    },
                    required = new[] { "count" }
                }
            },
            async args =>
            {
                var count = args?.GetProperty("count").GetInt32() ?? 1;
                var ids = playerManager.AddPlayers(count);
                await Task.CompletedTask;
                return McpResult.Text($"Created {count} players: [{string.Join(", ", ids)}]");
            });

        registry.Register(
            new McpToolDefinition
            {
                Name = "gdd_remove_player",
                Description = "Remove a browser instance by player ID.",
                InputSchema = new
                {
                    type = "object",
                    properties = new
                    {
                        player_id = new { type = "integer", description = "Player ID to remove" }
                    },
                    required = new[] { "player_id" }
                }
            },
            async args =>
            {
                var playerId = args?.GetProperty("player_id").GetInt32() ?? 0;
                var player = playerManager.GetPlayer(playerId);
                if (player is null)
                    return McpResult.Error($"Player {playerId} not found");
                playerManager.RemovePlayer(playerId);
                await Task.CompletedTask;
                return McpResult.Text($"Removed player {playerId}");
            });

        registry.Register(
            new McpToolDefinition
            {
                Name = "gdd_list_windows",
                Description = "List all active browser windows with their current state.",
                InputSchema = new { type = "object", properties = new { } }
            },
            async args =>
            {
                var players = playerManager.GetPlayers().Select(p => new
                {
                    id = p.PlayerId,
                    name = p.PlayerName,
                    url = p.CurrentUrl,
                    status = p.StatusText,
                    overlay_open = p.IsOverlayOpen
                });
                await Task.CompletedTask;
                return McpResult.Text(JsonSerializer.Serialize(players, new JsonSerializerOptions { WriteIndented = true }));
            });
    }
}
