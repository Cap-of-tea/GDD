using GDD.Abstractions;

namespace GDD.Mcp;

public static class McpResult
{
    public static async Task<McpToolResult> ElementNotFound(IPlayerContext player, string selector, string? suffix = null)
    {
        string readyState = "unknown";
        for (int i = 0; i < 3; i++)
        {
            readyState = await player.Engine!.ExecuteJavaScriptAsync("document.readyState");
            if (readyState.Contains("complete")) break;
            await Task.Delay(1000);
        }

        var message = suffix != null
            ? $"Element '{selector}' not found. {suffix}"
            : $"Element '{selector}' not found";

        if (!readyState.Contains("complete"))
        {
            return new McpToolResult
            {
                IsError = true,
                Content = new List<McpContent>
                {
                    new() { Type = "text", Text = $"{message}. Page still loading (readyState={readyState.Trim('"')})" }
                }
            };
        }

        var screenshot = await player.Engine!.CaptureScreenshotAsync(100);
        var base64 = Convert.ToBase64String(screenshot);

        return new McpToolResult
        {
            IsError = true,
            Content = new List<McpContent>
            {
                new() { Type = "text", Text = message },
                new() { Type = "image", Data = base64, MimeType = "image/jpeg" }
            }
        };
    }

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
