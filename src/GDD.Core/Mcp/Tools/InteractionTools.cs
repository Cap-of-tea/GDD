using System.Text.Json;
using GDD.Abstractions;

namespace GDD.Mcp.Tools;

public static class InteractionTools
{
    public static void Register(McpToolRegistry registry, IPlayerManager playerManager)
    {
        registry.Register(
            new McpToolDefinition
            {
                Name = "gdd_tap",
                Description = "Simulate a tap (touch) on an element by CSS selector or coordinates.",
                InputSchema = new
                {
                    type = "object",
                    properties = new
                    {
                        player_id = new { type = "integer", description = "Player ID" },
                        selector = new { type = "string", description = "CSS selector to tap (optional if x,y provided)" },
                        x = new { type = "number", description = "X coordinate (optional if selector provided)" },
                        y = new { type = "number", description = "Y coordinate (optional if selector provided)" }
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

                double x, y;

                if (args?.TryGetProperty("selector", out var selectorEl) == true)
                {
                    var selector = selectorEl.GetString()!.Replace("'", "\\'");
                    var rectJson = await player.Engine.ExecuteJavaScriptAsync(
                        $@"(function() {{ var el = document.querySelector('{selector}'); if (!el) return null; var r = el.getBoundingClientRect(); return JSON.stringify({{x: r.x + r.width/2, y: r.y + r.height/2}}); }})()");

                    if (rectJson == "null" || rectJson == "\"null\"")
                        return McpResult.Error($"Element '{selectorEl.GetString()}' not found");

                    var clean = rectJson.Trim('"').Replace("\\\"", "\"");
                    if (clean.StartsWith("\\"))
                        clean = JsonSerializer.Deserialize<string>(rectJson) ?? clean;
                    using var doc = JsonDocument.Parse(clean);
                    x = doc.RootElement.GetProperty("x").GetDouble();
                    y = doc.RootElement.GetProperty("y").GetDouble();
                }
                else
                {
                    x = args?.GetProperty("x").GetDouble() ?? 0;
                    y = args?.GetProperty("y").GetDouble() ?? 0;
                }

                var engine = player.Engine;
                var ts = DateTimeOffset.UtcNow.ToUnixTimeMilliseconds() / 1000.0;

                await engine.CallCdpMethodAsync("Input.dispatchTouchEvent",
                    JsonSerializer.Serialize(new
                    {
                        type = "touchStart",
                        touchPoints = new[] { new { x, y } },
                        modifiers = 0,
                        timestamp = ts
                    }));

                await Task.Delay(50);

                await engine.CallCdpMethodAsync("Input.dispatchTouchEvent",
                    JsonSerializer.Serialize(new
                    {
                        type = "touchEnd",
                        touchPoints = Array.Empty<object>(),
                        modifiers = 0,
                        timestamp = ts + 0.05
                    }));

                return McpResult.Text($"Tapped at ({x:F0}, {y:F0}) on player {playerId}");
            });

        registry.Register(
            new McpToolDefinition
            {
                Name = "gdd_swipe",
                Description = "Simulate a swipe gesture on a browser window.",
                InputSchema = new
                {
                    type = "object",
                    properties = new
                    {
                        player_id = new { type = "integer", description = "Player ID" },
                        direction = new { type = "string", @enum = new[] { "up", "down", "left", "right" }, description = "Swipe direction" },
                        distance = new { type = "integer", description = "Swipe distance in pixels (default 300)", @default = 300 }
                    },
                    required = new[] { "player_id", "direction" }
                }
            },
            async args =>
            {
                var playerId = args?.GetProperty("player_id").GetInt32() ?? 0;
                var direction = args?.GetProperty("direction").GetString() ?? "up";
                var distance = 300;
                if (args?.TryGetProperty("distance", out var distEl) == true)
                    distance = distEl.GetInt32();

                var player = playerManager.GetPlayer(playerId);
                if (player?.Engine is null)
                    return McpResult.Error($"Player {playerId} not found or not initialized");

                double startX = 195, startY = 422;
                double endX = startX, endY = startY;

                switch (direction)
                {
                    case "up": endY -= distance; break;
                    case "down": endY += distance; break;
                    case "left": endX -= distance; break;
                    case "right": endX += distance; break;
                }

                var engine = player.Engine;
                const int steps = 10;
                var ts = DateTimeOffset.UtcNow.ToUnixTimeMilliseconds() / 1000.0;

                await engine.CallCdpMethodAsync("Input.dispatchTouchEvent",
                    JsonSerializer.Serialize(new
                    {
                        type = "touchStart",
                        touchPoints = new[] { new { x = startX, y = startY } },
                        timestamp = ts
                    }));

                for (var i = 1; i <= steps; i++)
                {
                    var ratio = (double)i / steps;
                    var cx = startX + (endX - startX) * ratio;
                    var cy = startY + (endY - startY) * ratio;

                    await Task.Delay(16);
                    await engine.CallCdpMethodAsync("Input.dispatchTouchEvent",
                        JsonSerializer.Serialize(new
                        {
                            type = "touchMove",
                            touchPoints = new[] { new { x = cx, y = cy } },
                            timestamp = ts + i * 0.016
                        }));
                }

                await engine.CallCdpMethodAsync("Input.dispatchTouchEvent",
                    JsonSerializer.Serialize(new
                    {
                        type = "touchEnd",
                        touchPoints = Array.Empty<object>(),
                        timestamp = ts + steps * 0.016 + 0.01
                    }));

                return McpResult.Text($"Swiped {direction} {distance}px on player {playerId}");
            });

        registry.Register(
            new McpToolDefinition
            {
                Name = "gdd_scroll",
                Description = "Scroll inside a browser window, optionally targeting a specific element.",
                InputSchema = new
                {
                    type = "object",
                    properties = new
                    {
                        player_id = new { type = "integer", description = "Player ID" },
                        selector = new { type = "string", description = "CSS selector to scroll into view (optional)" },
                        direction = new { type = "string", @enum = new[] { "up", "down" }, description = "Scroll direction (if no selector)" },
                        amount = new { type = "integer", description = "Scroll amount in pixels (default 300)", @default = 300 }
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

                if (args?.TryGetProperty("selector", out var selectorEl) == true)
                {
                    var sel = selectorEl.GetString()!.Replace("'", "\\'");
                    await player.Engine.ExecuteJavaScriptAsync(
                        $"document.querySelector('{sel}')?.scrollIntoView({{behavior:'smooth',block:'center'}})");
                    return McpResult.Text($"Scrolled to '{selectorEl.GetString()}' on player {playerId}");
                }

                var direction = args?.TryGetProperty("direction", out var dirEl) == true ? dirEl.GetString() : "down";
                var amount = args?.TryGetProperty("amount", out var amtEl) == true ? amtEl.GetInt32() : 300;
                var delta = direction == "up" ? -amount : amount;

                await player.Engine.ExecuteJavaScriptAsync(
                    $"window.scrollBy({{top:{delta},behavior:'smooth'}})");

                return McpResult.Text($"Scrolled {direction} {amount}px on player {playerId}");
            });

        registry.Register(
            new McpToolDefinition
            {
                Name = "gdd_type",
                Description = "Type text into an input element identified by CSS selector.",
                InputSchema = new
                {
                    type = "object",
                    properties = new
                    {
                        player_id = new { type = "integer", description = "Player ID" },
                        selector = new { type = "string", description = "CSS selector of input element" },
                        text = new { type = "string", description = "Text to type" },
                        clear = new { type = "boolean", description = "Clear field before typing (default true)", @default = true }
                    },
                    required = new[] { "player_id", "selector", "text" }
                }
            },
            async args =>
            {
                var playerId = args?.GetProperty("player_id").GetInt32() ?? 0;
                var selector = args?.GetProperty("selector").GetString() ?? "";
                var text = args?.GetProperty("text").GetString() ?? "";
                var clear = true;
                if (args?.TryGetProperty("clear", out var clearEl) == true)
                    clear = clearEl.GetBoolean();

                var player = playerManager.GetPlayer(playerId);
                if (player?.Engine is null)
                    return McpResult.Error($"Player {playerId} not found or not initialized");

                var escapedSelector = selector.Replace("'", "\\'");
                var escapedText = JsonSerializer.Serialize(text);

                var script = $@"
                    (function() {{
                        var el = document.querySelector('{escapedSelector}');
                        if (!el) return 'not_found';
                        el.focus();
                        {(clear ? "el.value = '';" : "")}
                        var nativeInputValueSetter = Object.getOwnPropertyDescriptor(
                            window.HTMLInputElement.prototype, 'value')?.set
                            || Object.getOwnPropertyDescriptor(
                            window.HTMLTextAreaElement.prototype, 'value')?.set;
                        if (nativeInputValueSetter) nativeInputValueSetter.call(el, {(clear ? "" : "el.value + ")} {escapedText});
                        else el.value = {(clear ? "" : "el.value + ")} {escapedText};
                        el.dispatchEvent(new Event('input', {{ bubbles: true }}));
                        el.dispatchEvent(new Event('change', {{ bubbles: true }}));
                        return 'ok';
                    }})()";

                var result = await player.Engine.ExecuteJavaScriptAsync(script);
                if (result.Contains("not_found"))
                    return McpResult.Error($"Element '{selector}' not found");

                return McpResult.Text($"Typed into '{selector}' on player {playerId}");
            });
    }
}
