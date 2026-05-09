using System.Text.Json;
using GDD.Abstractions;
using GDD.Models;
using GDD.Services;

namespace GDD.Mcp.Tools;

public static class EmulationTools
{
    public static void Register(
        McpToolRegistry registry,
        IPlayerManager playerManager,
        DeviceEmulationService deviceService,
        LocationEmulationService locationService,
        NetworkEmulationService networkService,
        CdpService cdpService)
    {
        registry.Register(
            new McpToolDefinition
            {
                Name = "gdd_set_device",
                Description = "Set device emulation preset for a browser window.",
                InputSchema = new
                {
                    type = "object",
                    properties = new
                    {
                        player_id = new { type = "integer", description = "Player ID" },
                        preset = new
                        {
                            type = "string",
                            description = "Device preset name",
                            @enum = DevicePresets.All.Select(d => d.Name).ToArray()
                        }
                    },
                    required = new[] { "player_id", "preset" }
                }
            },
            async args =>
            {
                var playerId = args?.GetProperty("player_id").GetInt32() ?? 0;
                var presetName = args?.GetProperty("preset").GetString() ?? "";
                var player = playerManager.GetPlayer(playerId);
                if (player?.Engine is null)
                    return McpResult.Error($"Player {playerId} not found or not initialized");

                var preset = DevicePresets.All.FirstOrDefault(d =>
                    d.Name.Equals(presetName, StringComparison.OrdinalIgnoreCase));
                if (preset is null)
                    return McpResult.Error($"Unknown device preset: {presetName}. Available: {string.Join(", ", DevicePresets.All.Select(d => d.Name))}");

                await deviceService.ApplyAsync(player.Engine, preset);
                player.SelectedDevice = preset;
                return McpResult.Text($"Device set to {preset.Name} ({preset.Width}x{preset.Height}) for player {playerId}");
            });

        registry.Register(
            new McpToolDefinition
            {
                Name = "gdd_set_location",
                Description = "Set geolocation, timezone, and locale for a browser window.",
                InputSchema = new
                {
                    type = "object",
                    properties = new
                    {
                        player_id = new { type = "integer", description = "Player ID" },
                        preset = new
                        {
                            type = "string",
                            description = "City preset name (or 'custom' for manual coords)",
                            @enum = new[] { "Moscow", "Saint Petersburg", "New York", "London", "Tokyo", "custom" }
                        },
                        latitude = new { type = "number", description = "Custom latitude (only with preset='custom')" },
                        longitude = new { type = "number", description = "Custom longitude (only with preset='custom')" },
                        timezone = new { type = "string", description = "Custom timezone ID (only with preset='custom')" },
                        locale = new { type = "string", description = "Custom locale (only with preset='custom')" }
                    },
                    required = new[] { "player_id", "preset" }
                }
            },
            async args =>
            {
                var playerId = args?.GetProperty("player_id").GetInt32() ?? 0;
                var presetName = args?.GetProperty("preset").GetString() ?? "";
                var player = playerManager.GetPlayer(playerId);
                if (player?.Engine is null)
                    return McpResult.Error($"Player {playerId} not found or not initialized");

                LocationPreset? preset;

                if (presetName.Equals("custom", StringComparison.OrdinalIgnoreCase))
                {
                    var lat = args?.GetProperty("latitude").GetDouble() ?? 0;
                    var lon = args?.GetProperty("longitude").GetDouble() ?? 0;
                    var tz = args?.TryGetProperty("timezone", out var tzEl) == true ? tzEl.GetString() ?? "UTC" : "UTC";
                    var loc = args?.TryGetProperty("locale", out var locEl) == true ? locEl.GetString() ?? "en-US" : "en-US";
                    preset = new LocationPreset("Custom", lat, lon, 10, tz, loc);
                }
                else
                {
                    preset = LocationPresets.All.FirstOrDefault(l =>
                        l.CityName.Equals(presetName, StringComparison.OrdinalIgnoreCase));
                    if (preset is null)
                        return McpResult.Error($"Unknown location: {presetName}");
                }

                await locationService.ApplyAsync(player.Engine, preset);
                return McpResult.Text($"Location set to {preset.CityName} ({preset.Latitude}, {preset.Longitude}) for player {playerId}");
            });

        registry.Register(
            new McpToolDefinition
            {
                Name = "gdd_set_network",
                Description = "Set network condition emulation for a browser window.",
                InputSchema = new
                {
                    type = "object",
                    properties = new
                    {
                        player_id = new { type = "integer", description = "Player ID" },
                        preset = new
                        {
                            type = "string",
                            description = "Network condition preset",
                            @enum = new[] { "Online", "4G", "Fast 3G", "Slow 3G", "Offline" }
                        }
                    },
                    required = new[] { "player_id", "preset" }
                }
            },
            async args =>
            {
                var playerId = args?.GetProperty("player_id").GetInt32() ?? 0;
                var presetName = args?.GetProperty("preset").GetString() ?? "";
                var player = playerManager.GetPlayer(playerId);
                if (player?.Engine is null)
                    return McpResult.Error($"Player {playerId} not found or not initialized");

                var preset = NetworkPresets.All.FirstOrDefault(n =>
                    n.Name.Equals(presetName, StringComparison.OrdinalIgnoreCase));
                if (preset is null)
                    return McpResult.Error($"Unknown network preset: {presetName}");

                await networkService.ApplyAsync(player.Engine, preset);
                return McpResult.Text($"Network set to {preset.Name} for player {playerId}");
            });

        registry.Register(
            new McpToolDefinition
            {
                Name = "gdd_set_viewport",
                Description = "Set custom viewport size for a browser window (arbitrary width/height).",
                InputSchema = new
                {
                    type = "object",
                    properties = new
                    {
                        player_id = new { type = "integer", description = "Player ID" },
                        width = new { type = "integer", description = "Viewport width in pixels" },
                        height = new { type = "integer", description = "Viewport height in pixels" },
                        device_scale_factor = new { type = "number", description = "Device scale factor (default 1.0)" },
                        mobile = new { type = "boolean", description = "Emulate mobile device (default false)" },
                        user_agent = new { type = "string", description = "Custom user agent string" }
                    },
                    required = new[] { "player_id", "width", "height" }
                }
            },
            async args =>
            {
                var playerId = args?.GetProperty("player_id").GetInt32() ?? 0;
                var width = args?.GetProperty("width").GetInt32() ?? 375;
                var height = args?.GetProperty("height").GetInt32() ?? 667;
                var scale = args?.TryGetProperty("device_scale_factor", out var sEl) == true ? sEl.GetDouble() : 1.0;
                var mobile = args?.TryGetProperty("mobile", out var mEl) == true && mEl.GetBoolean();
                var ua = args?.TryGetProperty("user_agent", out var uaEl) == true ? uaEl.GetString() ?? "" : "";

                var player = playerManager.GetPlayer(playerId);
                if (player?.Engine is null)
                    return McpResult.Error($"Player {playerId} not found or not initialized");

                if (string.IsNullOrEmpty(ua))
                    ua = mobile
                        ? "Mozilla/5.0 (iPhone; CPU iPhone OS 18_0 like Mac OS X) AppleWebKit/605.1.15 (KHTML, like Gecko) Version/18.0 Mobile/15E148 Safari/604.1"
                        : "Mozilla/5.0 (Windows NT 10.0; Win64; x64) AppleWebKit/537.36 (KHTML, like Gecko) Chrome/128.0.0.0 Safari/537.36";

                var preset = new DevicePreset($"Custom {width}x{height}", "Custom", width, height, scale, ua, mobile, mobile);
                await deviceService.ApplyAsync(player.Engine, preset);
                player.SelectedDevice = preset;
                return McpResult.Text($"Viewport set to {width}x{height} (scale={scale}, mobile={mobile}) for player {playerId}");
            });

        registry.Register(
            new McpToolDefinition
            {
                Name = "gdd_set_language",
                Description = "Set browser language/locale. Changes navigator.language, navigator.languages, and Accept-Language header.",
                InputSchema = new
                {
                    type = "object",
                    properties = new
                    {
                        player_id = new { type = "integer", description = "Player ID" },
                        locale = new { type = "string", description = "Locale code (e.g. 'ru', 'en-US', 'ja-JP', 'de-DE')" }
                    },
                    required = new[] { "player_id", "locale" }
                }
            },
            async args =>
            {
                var playerId = args?.GetProperty("player_id").GetInt32() ?? 0;
                var locale = args?.GetProperty("locale").GetString() ?? "en-US";

                var player = playerManager.GetPlayer(playerId);
                if (player?.Engine is null)
                    return McpResult.Error($"Player {playerId} not found or not initialized");

                await cdpService.CallAsync(player.Engine, "Emulation.setLocaleOverride", new { locale });

                await cdpService.CallAsync(player.Engine, "Emulation.setUserAgentOverride", new
                {
                    userAgent = player.SelectedDevice.UserAgent,
                    acceptLanguage = locale
                });

                await cdpService.CallAsync(player.Engine, "Network.setExtraHTTPHeaders", new
                {
                    headers = new Dictionary<string, string> { ["Accept-Language"] = locale }
                });

                player.Language = locale;
                return McpResult.Text($"Language set to {locale} for player {playerId}");
            });
    }
}
