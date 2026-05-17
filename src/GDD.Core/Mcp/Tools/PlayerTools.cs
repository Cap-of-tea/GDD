using System.Text.Json;
using GDD.Abstractions;

namespace GDD.Mcp.Tools;

// NOTE: Players created via GUI (OwnerSessionId == null) are accessible to ALL MCP sessions.

public static class PlayerTools
{
    public static void Register(McpToolRegistry registry, IPlayerManager playerManager)
    {
        registry.Register(
            new McpToolDefinition
            {
                Name = "gdd_add_players",
                Description = "Create N new browser player instances. Each player is an isolated Chromium browser with its own profile, cookies, and storage. Returns the list of created player IDs. Optionally specify a device preset (default: iPhone 15 Pro). Maximum 64 players.",
                InputSchema = new
                {
                    type = "object",
                    properties = new
                    {
                        count = new { type = "integer", minimum = 1, maximum = 64, description = "Number of players to add" },
                        device = new
                        {
                            type = "string",
                            description = "Device preset name (default: iPhone 15 Pro)",
                            @enum = GDD.Models.DevicePresets.All.Select(d => d.Name).ToArray()
                        }
                    },
                    required = new[] { "count" }
                }
            },
            async args =>
            {
                var count = args?.GetProperty("count").GetInt32() ?? 1;
                var deviceName = args?.TryGetProperty("device", out var dEl) == true ? dEl.GetString() : null;
                var ids = playerManager.AddPlayers(count, deviceName, McpSessionContext.CurrentSessionId);
                await Task.CompletedTask;
                var suffix = string.IsNullOrEmpty(deviceName) ? "" : $" with {deviceName}";
                return McpResult.Text($"Created {count} players{suffix}: [{string.Join(", ", ids)}]");
            });

        registry.Register(
            new McpToolDefinition
            {
                Name = "gdd_remove_player",
                Description = "Remove a browser player instance and close its Chromium process. Frees all resources including the browser profile, console/network logs, and notification subscriptions. The player ID cannot be reused.",
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
                Description = "List all active browser players with their current state. Returns JSON array with player ID, name, current URL, status, overlay visibility, and owning MCP session. Use to discover available players before performing actions.",
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
                    overlay_open = p.IsOverlayOpen,
                    session = p.OwnerSessionId is not null ? p.OwnerSessionId[..8] : "shared"
                });
                await Task.CompletedTask;
                return McpResult.Text(JsonSerializer.Serialize(players, new JsonSerializerOptions { WriteIndented = true }));
            });
    }
}
