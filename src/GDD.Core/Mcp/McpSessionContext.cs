namespace GDD.Mcp;

public static class McpSessionContext
{
    private static readonly AsyncLocal<string?> _sessionId = new();

    public static string? CurrentSessionId
    {
        get => _sessionId.Value;
        set => _sessionId.Value = value;
    }
}
