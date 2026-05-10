using System.Text.Json;
using GDD.Abstractions;

namespace GDD.Mcp;

public sealed class McpToolRegistry
{
    private readonly Dictionary<string, McpToolDefinition> _definitions = new();
    private readonly Dictionary<string, Func<JsonElement?, Task<McpToolResult>>> _handlers = new();
    private IPlayerManager? _playerManager;

    public void SetPlayerManager(IPlayerManager playerManager) =>
        _playerManager = playerManager;

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
        if (_playerManager is null || result.IsError)
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

        var beacon = $"\n⚠ {string.Join(". ", warnings)}. Use gdd_get_console(player_id) to inspect.";
        var textContent = result.Content.FirstOrDefault(c => c.Type == "text");
        if (textContent is not null)
            textContent.Text += beacon;
    }
}
