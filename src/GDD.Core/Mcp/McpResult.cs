namespace GDD.Mcp;

public static class McpResult
{
    public static McpToolResult Text(string text)
    {
        return new McpToolResult
        {
            Content = new List<McpContent>
            {
                new() { Type = "text", Text = text }
            }
        };
    }

    public static McpToolResult Error(string message)
    {
        return new McpToolResult
        {
            IsError = true,
            Content = new List<McpContent>
            {
                new() { Type = "text", Text = message }
            }
        };
    }

    public static McpToolResult Image(string base64, string mimeType = "image/png")
    {
        return new McpToolResult
        {
            Content = new List<McpContent>
            {
                new() { Type = "image", Data = base64, MimeType = mimeType }
            }
        };
    }
}
