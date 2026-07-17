using System.Text.Json;
using GDD.Abstractions;
using GDD.Services;

namespace GDD.Mcp.Tools;

public static class InterceptionTools
{
    public static void Register(
        McpToolRegistry registry,
        IPlayerManager playerManager,
        RequestInterceptionService interception)
    {
        registry.Register(
            new McpToolDefinition
            {
                Name = "gdd_set_headers",
                Description = "Rewrite response headers for a browser player. Set allow_framing=true to strip " +
                              "X-Frame-Options and the CSP frame-ancestors directive so a site that normally " +
                              "refuses to be embedded can be loaded in an iframe (the rest of its CSP is kept). " +
                              "Also takes strip_response / set_response for arbitrary header edits. " +
                              "Applies to page navigations from this point on — call gdd_navigate or gdd_reload " +
                              "after enabling. Call again with no rules to turn interception off.",
                InputSchema = new
                {
                    type = "object",
                    properties = new
                    {
                        player_id = new { type = "integer", description = "Player ID" },
                        allow_framing = new
                        {
                            type = "boolean",
                            description = "Strip X-Frame-Options and CSP frame-ancestors so the page can be framed. Default: false"
                        },
                        strip_response = new
                        {
                            type = "array",
                            items = new { type = "string" },
                            description = "Response header names to remove (case-insensitive)"
                        },
                        set_response = new
                        {
                            type = "object",
                            additionalProperties = new { type = "string" },
                            description = "Response headers to add or replace, as {\"name\": \"value\"}"
                        },
                        url_pattern = new
                        {
                            type = "string",
                            description = "URL glob to intercept (default '*')"
                        }
                    },
                    required = new[] { "player_id" }
                },
                Annotations = new { readOnlyHint = false, destructiveHint = false, idempotentHint = true, openWorldHint = false }
            },
            async args =>
            {
                var playerId = args?.GetProperty("player_id").GetInt32() ?? 0;

                var allowFraming = args?.TryGetProperty("allow_framing", out var afEl) == true &&
                                   afEl.ValueKind == JsonValueKind.True;

                var strip = new List<string>();
                if (args?.TryGetProperty("strip_response", out var stripEl) == true &&
                    stripEl.ValueKind == JsonValueKind.Array)
                {
                    foreach (var item in stripEl.EnumerateArray())
                    {
                        var name = item.GetString();
                        if (!string.IsNullOrWhiteSpace(name)) strip.Add(name.Trim());
                    }
                }

                var set = new Dictionary<string, string>(StringComparer.OrdinalIgnoreCase);
                if (args?.TryGetProperty("set_response", out var setEl) == true &&
                    setEl.ValueKind == JsonValueKind.Object)
                {
                    foreach (var prop in setEl.EnumerateObject())
                        set[prop.Name] = prop.Value.ValueKind == JsonValueKind.String
                            ? prop.Value.GetString() ?? ""
                            : prop.Value.ToString();
                }

                var urlPattern = args?.TryGetProperty("url_pattern", out var upEl) == true
                    ? upEl.GetString() ?? "*"
                    : "*";
                if (string.IsNullOrWhiteSpace(urlPattern)) urlPattern = "*";

                var player = await playerManager.GetReadyPlayerAsync(playerId);
                if (player?.Engine is null)
                    return McpResult.Error($"Player {playerId} not found or not initialized");

                var rules = new InterceptionRules
                {
                    AllowFraming = allowFraming,
                    StripResponseHeaders = strip,
                    SetResponseHeaders = set,
                    UrlPattern = urlPattern
                };

                await interception.ApplyAsync(player.Engine, playerId, rules);

                if (rules.IsNoop)
                    return McpResult.Text($"Header interception disabled for player {playerId}");

                var parts = new List<string>();
                if (allowFraming) parts.Add("allow_framing (X-Frame-Options + CSP frame-ancestors stripped)");
                if (strip.Count > 0) parts.Add($"strip [{string.Join(", ", strip)}]");
                if (set.Count > 0) parts.Add($"set [{string.Join(", ", set.Keys)}]");

                return McpResult.Text(
                    $"Header interception active for player {playerId} on '{urlPattern}': {string.Join("; ", parts)}. " +
                    "Navigate or reload for it to take effect.");
            });
    }
}
