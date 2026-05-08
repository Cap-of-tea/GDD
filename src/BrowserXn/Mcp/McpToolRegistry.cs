using System.Text.Json;

namespace GDD.Mcp;

public sealed class McpToolRegistry
{
    private readonly Dictionary<string, McpToolDefinition> _definitions = new();
    private readonly Dictionary<string, Func<JsonElement?, Task<McpToolResult>>> _handlers = new();

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
            return await handler(arguments);
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
}
