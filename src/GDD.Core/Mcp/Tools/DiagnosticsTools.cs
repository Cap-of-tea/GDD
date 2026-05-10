using System.Text.Json;
using GDD.Abstractions;
using GDD.Services;

namespace GDD.Mcp.Tools;

public static class DiagnosticsTools
{
    public static void Register(
        McpToolRegistry registry,
        IPlayerManager playerManager,
        ConsoleInterceptionService consoleService,
        NetworkMonitoringService networkMonitorService,
        CdpService cdpService)
    {
        registry.Register(
            new McpToolDefinition
            {
                Name = "gdd_get_console",
                Description = "Get console output (log/warn/error/info/debug) and uncaught exceptions from a browser window.",
                InputSchema = new
                {
                    type = "object",
                    properties = new
                    {
                        player_id = new { type = "integer", description = "Player ID" },
                        level = new { type = "string", description = "Filter by level (log/warn/error/info/debug)" },
                        last = new { type = "integer", description = "Return only last N entries" }
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

                string? level = null;
                if (args?.TryGetProperty("level", out var levelEl) == true)
                    level = levelEl.GetString();

                var entries = consoleService.GetEntries(playerId, level);

                if (args?.TryGetProperty("last", out var lastEl) == true)
                {
                    var last = lastEl.GetInt32();
                    if (last > 0 && last < entries.Count)
                        entries = entries.Skip(entries.Count - last).ToList();
                }

                var result = entries.Select(e => new
                {
                    level = e.Level,
                    message = e.Message,
                    source = e.Source,
                    line = e.LineNumber,
                    column = e.ColumnNumber,
                    stack_trace = e.StackTrace,
                    is_exception = e.IsException,
                    timestamp = e.Timestamp.ToString("o")
                });

                await Task.CompletedTask;
                return McpResult.Text(JsonSerializer.Serialize(result, new JsonSerializerOptions { WriteIndented = true }));
            });

        registry.Register(
            new McpToolDefinition
            {
                Name = "gdd_get_network",
                Description = "Get network requests from a browser window. Shows method, URL, status, timing, and errors.",
                InputSchema = new
                {
                    type = "object",
                    properties = new
                    {
                        player_id = new { type = "integer", description = "Player ID" },
                        failed_only = new { type = "boolean", description = "Show only failed requests" },
                        resource_type = new { type = "string", description = "Filter by resource type (Document/Script/Stylesheet/Image/XHR/Fetch/Font/Media/Other)" },
                        last = new { type = "integer", description = "Return only last N entries" }
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

                var failedOnly = args?.TryGetProperty("failed_only", out var foEl) == true && foEl.GetBoolean();
                string? resourceType = null;
                if (args?.TryGetProperty("resource_type", out var rtEl) == true)
                    resourceType = rtEl.GetString();

                var entries = networkMonitorService.GetEntries(playerId, failedOnly, resourceType);

                if (args?.TryGetProperty("last", out var lastEl) == true)
                {
                    var last = lastEl.GetInt32();
                    if (last > 0 && last < entries.Count)
                        entries = entries.Skip(entries.Count - last).ToList();
                }

                var result = entries.Select(e => new
                {
                    method = e.Method,
                    url = e.Url,
                    resource_type = e.ResourceType,
                    status = e.StatusCode,
                    status_text = e.StatusText,
                    mime_type = e.MimeType,
                    content_length = e.ContentLength,
                    duration_ms = e.DurationMs.HasValue ? Math.Round(e.DurationMs.Value, 1) : (double?)null,
                    error = e.ErrorText,
                    failed = e.Failed,
                    timestamp = e.RequestTime.ToString("o")
                });

                await Task.CompletedTask;
                return McpResult.Text(JsonSerializer.Serialize(result, new JsonSerializerOptions { WriteIndented = true }));
            });

        registry.Register(
            new McpToolDefinition
            {
                Name = "gdd_get_performance",
                Description = "Get performance metrics (JS heap, DOM nodes, frames, task duration) from a browser window via CDP.",
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

                await cdpService.CallAsync(player.Engine, "Performance.enable", new { });
                var json = await cdpService.CallWithResultAsync(
                    player.Engine, "Performance.getMetrics", new { });

                using var doc = JsonDocument.Parse(json);
                var metrics = doc.RootElement.GetProperty("metrics");

                var result = new Dictionary<string, double>();
                foreach (var m in metrics.EnumerateArray())
                {
                    var name = m.GetProperty("name").GetString() ?? "";
                    var value = m.GetProperty("value").GetDouble();
                    result[name] = value;
                }

                return McpResult.Text(JsonSerializer.Serialize(result, new JsonSerializerOptions { WriteIndented = true }));
            });

        registry.Register(
            new McpToolDefinition
            {
                Name = "gdd_clear_logs",
                Description = "Clear console and/or network logs for a browser window.",
                InputSchema = new
                {
                    type = "object",
                    properties = new
                    {
                        player_id = new { type = "integer", description = "Player ID" },
                        target = new
                        {
                            type = "string",
                            description = "What to clear: console, network, or all",
                            @enum = new[] { "console", "network", "all" }
                        }
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

                var target = "all";
                if (args?.TryGetProperty("target", out var tEl) == true)
                    target = tEl.GetString() ?? "all";

                if (target is "console" or "all")
                {
                    consoleService.Clear(playerId);
                    player.ConsoleErrorCount = 0;
                    player.LastError = "";
                }

                if (target is "network" or "all")
                {
                    networkMonitorService.Clear(playerId);
                    player.NetworkErrorCount = 0;
                }

                await Task.CompletedTask;
                return McpResult.Text($"Cleared {target} logs for player {playerId}");
            });

        registry.Register(
            new McpToolDefinition
            {
                Name = "gdd_storage",
                Description = "Read, write, or clear localStorage/sessionStorage.",
                InputSchema = new
                {
                    type = "object",
                    properties = new
                    {
                        player_id = new { type = "integer", description = "Player ID" },
                        action = new
                        {
                            type = "string",
                            description = "Action to perform",
                            @enum = new[] { "get", "set", "remove", "clear", "keys" }
                        },
                        storage = new
                        {
                            type = "string",
                            description = "Storage type (default: local)",
                            @enum = new[] { "local", "session" }
                        },
                        key = new { type = "string", description = "Key name (for get/set/remove)" },
                        value = new { type = "string", description = "Value to set (for set action)" }
                    },
                    required = new[] { "player_id", "action" }
                }
            },
            async args =>
            {
                var playerId = args?.GetProperty("player_id").GetInt32() ?? 0;
                var action = args?.GetProperty("action").GetString() ?? "keys";
                var storageType = args?.TryGetProperty("storage", out var stEl) == true
                    ? stEl.GetString() ?? "local" : "local";
                var player = playerManager.GetPlayer(playerId);
                if (player?.Engine is null)
                    return McpResult.Error($"Player {playerId} not found or not initialized");

                var store = storageType == "session" ? "sessionStorage" : "localStorage";

                switch (action)
                {
                    case "get":
                    {
                        var key = args?.TryGetProperty("key", out var kEl) == true ? kEl.GetString() : null;
                        if (string.IsNullOrEmpty(key))
                            return McpResult.Error("'key' is required for get action");
                        var escapedKey = JsonSerializer.Serialize(key);
                        var result = await player.Engine.ExecuteJavaScriptAsync($"{store}.getItem({escapedKey})");
                        return McpResult.Text(result);
                    }
                    case "set":
                    {
                        var key = args?.TryGetProperty("key", out var kEl) == true ? kEl.GetString() : null;
                        var value = args?.TryGetProperty("value", out var vEl) == true ? vEl.GetString() : null;
                        if (string.IsNullOrEmpty(key))
                            return McpResult.Error("'key' is required for set action");
                        var ek = JsonSerializer.Serialize(key);
                        var ev = JsonSerializer.Serialize(value ?? "");
                        await player.Engine.ExecuteJavaScriptAsync($"{store}.setItem({ek}, {ev})");
                        return McpResult.Text($"Set {store}[{key}] on player {playerId}");
                    }
                    case "remove":
                    {
                        var key = args?.TryGetProperty("key", out var kEl) == true ? kEl.GetString() : null;
                        if (string.IsNullOrEmpty(key))
                            return McpResult.Error("'key' is required for remove action");
                        var ek = JsonSerializer.Serialize(key);
                        await player.Engine.ExecuteJavaScriptAsync($"{store}.removeItem({ek})");
                        return McpResult.Text($"Removed {store}[{key}] on player {playerId}");
                    }
                    case "clear":
                        await player.Engine.ExecuteJavaScriptAsync($"{store}.clear()");
                        return McpResult.Text($"Cleared {store} on player {playerId}");
                    case "keys":
                    {
                        var result = await player.Engine.ExecuteJavaScriptAsync(
                            $"JSON.stringify(Object.keys({store}))");
                        return McpResult.Text(result);
                    }
                    default:
                        return McpResult.Error($"Unknown action: {action}");
                }
            });

        registry.Register(
            new McpToolDefinition
            {
                Name = "gdd_cookies",
                Description = "Read or clear browser cookies for the current page.",
                InputSchema = new
                {
                    type = "object",
                    properties = new
                    {
                        player_id = new { type = "integer", description = "Player ID" },
                        action = new
                        {
                            type = "string",
                            description = "Action to perform",
                            @enum = new[] { "get", "clear" }
                        },
                        name = new { type = "string", description = "Filter by cookie name (optional, for get)" }
                    },
                    required = new[] { "player_id", "action" }
                }
            },
            async args =>
            {
                var playerId = args?.GetProperty("player_id").GetInt32() ?? 0;
                var action = args?.GetProperty("action").GetString() ?? "get";
                var player = playerManager.GetPlayer(playerId);
                if (player?.Engine is null)
                    return McpResult.Error($"Player {playerId} not found or not initialized");

                if (action == "clear")
                {
                    var cookiesJson = await cdpService.CallWithResultAsync(
                        player.Engine, "Network.getAllCookies", new { });
                    using var cDoc = JsonDocument.Parse(cookiesJson);
                    var cookies = cDoc.RootElement.GetProperty("cookies");
                    foreach (var c in cookies.EnumerateArray())
                    {
                        var cName = c.GetProperty("name").GetString();
                        var cDomain = c.GetProperty("domain").GetString();
                        await cdpService.CallAsync(player.Engine, "Network.deleteCookies",
                            new { name = cName, domain = cDomain });
                    }
                    return McpResult.Text($"Cleared {cookies.GetArrayLength()} cookies on player {playerId}");
                }

                var json = await cdpService.CallWithResultAsync(
                    player.Engine, "Network.getAllCookies", new { });
                using var doc = JsonDocument.Parse(json);
                var allCookies = doc.RootElement.GetProperty("cookies");

                string? filterName = null;
                if (args?.TryGetProperty("name", out var nEl) == true)
                    filterName = nEl.GetString();

                var result = new List<object>();
                foreach (var c in allCookies.EnumerateArray())
                {
                    var n = c.GetProperty("name").GetString() ?? "";
                    if (filterName is not null && !n.Equals(filterName, StringComparison.OrdinalIgnoreCase))
                        continue;
                    result.Add(new
                    {
                        name = n,
                        value = c.GetProperty("value").GetString(),
                        domain = c.GetProperty("domain").GetString(),
                        path = c.GetProperty("path").GetString(),
                        httpOnly = c.GetProperty("httpOnly").GetBoolean(),
                        secure = c.GetProperty("secure").GetBoolean()
                    });
                }

                return McpResult.Text(JsonSerializer.Serialize(result, new JsonSerializerOptions { WriteIndented = true }));
            });
    }
}
