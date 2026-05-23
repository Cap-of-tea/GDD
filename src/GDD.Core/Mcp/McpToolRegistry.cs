using System.Text.Json;
using GDD.Abstractions;
using GDD.Services;

namespace GDD.Mcp;

public sealed class McpToolRegistry
{
    private readonly Dictionary<string, McpToolDefinition> _definitions = new();
    private readonly Dictionary<string, Func<JsonElement?, Task<McpToolResult>>> _handlers = new();
    private IPlayerManager? _playerManager;
    private UpdateService? _updateService;

    public void SetPlayerManager(IPlayerManager playerManager) =>
        _playerManager = playerManager;

    public void SetUpdateService(UpdateService updateService) =>
        _updateService = updateService;

    public void Register(McpToolDefinition definition, Func<JsonElement?, Task<McpToolResult>> handler)
    {
        _definitions[definition.Name] = definition;
        _handlers[definition.Name] = handler;
    }

    public IReadOnlyCollection<McpToolDefinition> GetDefinitions() => _definitions.Values;

    public async Task<McpToolResult> InvokeAsync(string toolName, JsonElement? arguments)
    {
        if (!_handlers.TryGetValue(toolName, out var handler))
        {
            return new McpToolResult
            {
                IsError = true,
                Content = new List<McpContent>
                {
                    new() { Type = "text", Text = $"Unknown tool: {toolName}" }
                }
            };
        }

        try
        {
            var result = await handler(arguments);
            AppendErrorBeacon(result, toolName);
            AppendUpdateBeacon(result, toolName);
            return result;
        }
        catch (Exception ex)
        {
            return new McpToolResult
            {
                IsError = true,
                Content = new List<McpContent>
                {
                    new() { Type = "text", Text = $"Error: {ex.Message}" }
                }
            };
        }
    }

    private void AppendErrorBeacon(McpToolResult result, string toolName)
    {
        if (_playerManager is null)
            return;
        if (toolName is "gdd_get_console" or "gdd_clear_logs" or "gdd_get_state")
            return;

        var players = _playerManager.GetPlayers();
        var warnings = new List<string>();
        foreach (var p in players)
        {
            if (p.ConsoleErrorCount > 0)
                warnings.Add($"Player {p.PlayerId}: {p.ConsoleErrorCount} console error{(p.ConsoleErrorCount > 1 ? "s" : "")}");
        }

        if (warnings.Count == 0)
            return;

        var beacon = $"⚠ {string.Join(". ", warnings)}. Use gdd_get_console(player_id) to inspect.\n";
        var textContent = result.Content.FirstOrDefault(c => c.Type == "text");
        if (textContent is not null)
            textContent.Text = beacon + textContent.Text;
    }

    private void AppendUpdateBeacon(McpToolResult result, string toolName)
    {
        if (_updateService is null) return;
        if (toolName is "gdd_check_update" or "gdd_update") return;

        _ = _updateService.CheckForUpdateAsync();

        if (!_updateService.ShouldShowUpdateBeacon()) return;

        var update = _updateService.CachedUpdate!;
        var beacon = $"🔄 GDD update: v{GddVersion.Current} → v{update.Version} ({update.SizeBytes / 1048576.0:F1} MB). Ask the user if they want to update, then call gdd_update(confirm=true).\n";
        var textContent = result.Content.FirstOrDefault(c => c.Type == "text");
        if (textContent is not null)
            textContent.Text = beacon + textContent.Text;
    }
}
