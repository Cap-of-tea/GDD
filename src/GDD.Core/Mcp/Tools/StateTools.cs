using System.Text.Json;
using GDD.Abstractions;
using GDD.Services;

namespace GDD.Mcp.Tools;

public static class StateTools
{
    public static void Register(
        McpToolRegistry registry,
        IPlayerManager playerManager,
        NotificationInterceptionService notificationService)
    {
        registry.Register(
            new McpToolDefinition
            {
                Name = "gdd_get_state",
                Description = "Get the current state of a browser window including URL, title, auth status, device, and network.",
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
                if (player is null)
                    return McpResult.Error($"Player {playerId} not found");

                var state = new
                {
                    player_id = player.PlayerId,
                    player_name = player.PlayerName,
                    url = player.CurrentUrl,
                    status = player.StatusText,
                    overlay_open = player.IsOverlayOpen,
                    network_status = player.NetworkStatus,
                    notification_count = player.NotificationCount,
                    device = new
                    {
                        name = player.SelectedDevice.Name,
                        width = player.SelectedDevice.Width,
                        height = player.SelectedDevice.Height,
                        scale = player.SelectedDevice.DeviceScaleFactor,
                        mobile = player.SelectedDevice.IsMobile
                    },
                    console_error_count = player.ConsoleErrorCount,
                    network_error_count = player.NetworkErrorCount,
                    last_error = player.LastError,
                    language = player.Language
                };

                await Task.CompletedTask;
                return McpResult.Text(JsonSerializer.Serialize(state, new JsonSerializerOptions { WriteIndented = true }));
            });

        registry.Register(
            new McpToolDefinition
            {
                Name = "gdd_get_notifications",
                Description = "Get received push notifications, optionally filtered by player.",
                InputSchema = new
                {
                    type = "object",
                    properties = new
                    {
                        player_id = new { type = "integer", description = "Player ID (0 for all)" }
                    }
                }
            },
            async args =>
            {
                var playerId = 0;
                if (args?.TryGetProperty("player_id", out var idEl) == true)
                    playerId = idEl.GetInt32();

                var notifications = notificationService.GetNotifications(playerId == 0 ? null : playerId);

                var result = notifications.Select(n => new
                {
                    player_id = n.PlayerId,
                    title = n.Title,
                    body = n.Body,
                    tag = n.Tag,
                    received_at = n.ReceivedAt.ToString("o")
                });

                await Task.CompletedTask;
                return McpResult.Text(JsonSerializer.Serialize(result, new JsonSerializerOptions { WriteIndented = true }));
            });
    }
}
