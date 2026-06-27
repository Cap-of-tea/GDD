using System.Collections.Concurrent;
using System.Text.Json;
using GDD.Abstractions;
using GDD.Core.Services;

namespace GDD.Mcp.Tools;

public static class InteractionTools
{
    // Last cursor position per player, so a humanized tap can travel continuously from
    // where the pointer was rather than teleporting to a fresh random start each time.
    // Player IDs are monotonic (never reused), so no stale-entry cleanup is needed.
    private static readonly ConcurrentDictionary<int, (double X, double Y)> _lastPointer = new();

    public static void Register(McpToolRegistry registry, IPlayerManager playerManager)
    {
        registry.Register(
            new McpToolDefinition
            {
                Name = "gdd_tap",
                Description = "Tap on an element by CSS selector or at exact (x, y) coordinates in CSS pixels. Sends a single, device-appropriate input — a touch tap on touch devices, a mouse click on desktop — never both, so it won't double-activate toggle UIs (menus, popovers, modals). Provide either a selector (element center is tapped) or x,y coordinates from a gdd_screenshot. Set humanize=true for human-like input — a natural mouse curve on desktop, or a small landing wobble with a varied hold on touch.",
                InputSchema = new
                {
                    type = "object",
                    properties = new
                    {
                        player_id = new { type = "integer", description = "Player ID" },
                        selector = new { type = "string", description = "CSS selector to tap (optional if x,y provided)" },
                        x = new { type = "number", description = "X coordinate (optional if selector provided)" },
                        y = new { type = "number", description = "Y coordinate (optional if selector provided)" },
                        humanize = new { type = "boolean", description = "Human-like input: a natural mouse curve before clicking on desktop, or a small landing wobble + varied hold on touch. Default: false" }
                    },
                    required = new[] { "player_id" }
                },
                Annotations = new { readOnlyHint = false, destructiveHint = false, idempotentHint = false, openWorldHint = false }
            },
            async args =>
            {
                var playerId = args?.GetProperty("player_id").GetInt32() ?? 0;
                var player = await playerManager.GetReadyPlayerAsync(playerId);
                if (player?.Engine is null)
                    return McpResult.Error($"Player {playerId} not found or not initialized");

                double x, y;

                if (args?.TryGetProperty("selector", out var selectorEl) == true)
                {
                    var selector = selectorEl.GetString()!.Replace("'", "\\'");
                    var rectJson = await player.Engine.ExecuteJavaScriptAsync(
                        $@"(function() {{ var el = document.querySelector('{selector}'); if (!el) return null; var r = el.getBoundingClientRect(); return {{x: r.x + r.width/2, y: r.y + r.height/2}}; }})()");

                    if (rectJson == "null" || rectJson == "\"null\"")
                        return await McpResult.ElementNotFound(player, selectorEl.GetString()!);

                    var json = rectJson;
                    if (json.StartsWith("\""))
                        json = JsonSerializer.Deserialize<string>(json) ?? json;
                    using var doc = JsonDocument.Parse(json);
                    x = doc.RootElement.GetProperty("x").GetDouble();
                    y = doc.RootElement.GetProperty("y").GetDouble();
                }
                else
                {
                    x = args?.GetProperty("x").GetDouble() ?? 0;
                    y = args?.GetProperty("y").GetDouble() ?? 0;
                }

                var engine = player.Engine;
                var humanize = args?.TryGetProperty("humanize", out var hEl) == true && hEl.GetBoolean();

                // A real tap is EITHER touch OR mouse — never both. Chromium already synthesizes
                // a compatibility click from a touch sequence, so ALSO dispatching a mouse click
                // activates the element twice: toggle UIs (menus, popovers, modals) open on the
                // first activation and close on the second. Pick the input that matches the
                // emulated device.
                if (player.SelectedDevice.HasTouch)
                {
                    var ts = DateTimeOffset.UtcNow.ToUnixTimeMilliseconds() / 1000.0;
                    await engine.CallCdpMethodAsync("Input.dispatchTouchEvent",
                        JsonSerializer.Serialize(new
                        {
                            type = "touchStart",
                            touchPoints = new[] { new { x, y } },
                            modifiers = 0,
                            timestamp = ts
                        }));

                    if (humanize)
                    {
                        // A finger doesn't travel across the screen like a cursor, so instead of
                        // the mouse curve, humanize a tap with a small landing wobble (kept within
                        // the ~8px tap slop so it stays a tap, not a drag) and a randomized hold.
                        var rng = Random.Shared;
                        var wobble = rng.Next(2, 4);
                        for (var i = 1; i <= wobble; i++)
                        {
                            await Task.Delay(rng.Next(15, 35));
                            await engine.CallCdpMethodAsync("Input.dispatchTouchEvent",
                                JsonSerializer.Serialize(new
                                {
                                    type = "touchMove",
                                    touchPoints = new[] { new { x = x + (rng.NextDouble() * 4 - 2), y = y + (rng.NextDouble() * 4 - 2) } },
                                    modifiers = 0,
                                    timestamp = ts + i * 0.02
                                }));
                        }
                        await Task.Delay(rng.Next(40, 110));
                    }
                    else
                    {
                        await Task.Delay(50);
                    }

                    await engine.CallCdpMethodAsync("Input.dispatchTouchEvent",
                        JsonSerializer.Serialize(new
                        {
                            type = "touchEnd",
                            touchPoints = Array.Empty<object>(),
                            modifiers = 0,
                            timestamp = ts + 0.05
                        }));
                }
                else
                {
                    if (humanize)
                    {
                        // Travel from where the cursor last was — a continuous human path
                        // across clicks, not a fresh random teleport each time. Only the very
                        // first move starts from a random point in the actual viewport.
                        var start = _lastPointer.TryGetValue(playerId, out var lp)
                            ? lp
                            : MouseMovementService.RandomStart(x, y, player.SelectedDevice.Width, player.SelectedDevice.Height);
                        var path = MouseMovementService.GeneratePath(start.X, start.Y, x, y);
                        foreach (var pt in path)
                        {
                            await engine.CallCdpMethodAsync("Input.dispatchMouseEvent",
                                JsonSerializer.Serialize(new { type = "mouseMoved", x = pt.X, y = pt.Y, button = "none", buttons = 0 }));
                            await Task.Delay(pt.DelayMs);
                        }
                    }
                    else
                    {
                        await engine.CallCdpMethodAsync("Input.dispatchMouseEvent",
                            JsonSerializer.Serialize(new { type = "mouseMoved", x, y, button = "none", buttons = 0 }));
                    }

                    await engine.CallCdpMethodAsync("Input.dispatchMouseEvent",
                        JsonSerializer.Serialize(new { type = "mousePressed", x, y, button = "left", buttons = 1, clickCount = 1 }));
                    await engine.CallCdpMethodAsync("Input.dispatchMouseEvent",
                        JsonSerializer.Serialize(new { type = "mouseReleased", x, y, button = "left", buttons = 0, clickCount = 1 }));

                    _lastPointer[playerId] = (x, y); // remember cursor for the next move's continuity
                }

                var inputKind = (player.SelectedDevice.HasTouch ? "touch" : "mouse") + (humanize ? ", humanized" : "");
                return McpResult.Text($"Tapped at ({x:F0}, {y:F0}) on player {playerId} ({inputKind})");
            });

        registry.Register(
            new McpToolDefinition
            {
                Name = "gdd_swipe",
                Description = "Simulate a swipe gesture on a browser player. Dispatches a series of touch events from the viewport center in the specified direction. Use for scrolling mobile content, dismissing panels, or navigating carousels.",
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
                },
                Annotations = new { readOnlyHint = false, destructiveHint = false, idempotentHint = false, openWorldHint = false }
            },
            async args =>
            {
                var playerId = args?.GetProperty("player_id").GetInt32() ?? 0;
                var direction = args?.GetProperty("direction").GetString() ?? "up";
                var distance = 300;
                if (args?.TryGetProperty("distance", out var distEl) == true)
                    distance = distEl.GetInt32();

                var player = await playerManager.GetReadyPlayerAsync(playerId);
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
                Name = "gdd_drag",
                Description = "Drag an element (CSS selector) and drop it at (x, y) CSS-pixel coordinates, or onto a target_selector. Uses a real press → move → release mouse sequence, which Chromium delivers as trusted pointerdown/pointermove/pointerup — so it drives pointer-based drag libraries (e.g. dnd-kit) and HTML5 drag-and-drop, unlike gdd_swipe (touch). For sensors with a delay activation constraint, raise hold_ms.",
                InputSchema = new
                {
                    type = "object",
                    properties = new
                    {
                        player_id = new { type = "integer", description = "Player ID" },
                        selector = new { type = "string", description = "CSS selector of the element to drag (its center is the grab point)" },
                        x = new { type = "number", description = "Drop target X in CSS pixels (optional if target_selector given)" },
                        y = new { type = "number", description = "Drop target Y in CSS pixels (optional if target_selector given)" },
                        target_selector = new { type = "string", description = "Optional CSS selector to drop onto (its center); overrides x,y" },
                        steps = new { type = "integer", description = "Intermediate move events (default 20)" },
                        hold_ms = new { type = "integer", description = "Pause after press before moving, ms (default 120; raise for delay-based sensors)" }
                    },
                    required = new[] { "player_id", "selector" }
                },
                Annotations = new { readOnlyHint = false, destructiveHint = false, idempotentHint = false, openWorldHint = false }
            },
            async args =>
            {
                var playerId = args?.GetProperty("player_id").GetInt32() ?? 0;
                var player = await playerManager.GetReadyPlayerAsync(playerId);
                if (player?.Engine is null)
                    return McpResult.Error($"Player {playerId} not found or not initialized");

                var engine = player.Engine;

                if (args?.TryGetProperty("selector", out var srcEl) != true || srcEl.GetString() is not { } selector)
                    return McpResult.Error("selector is required");

                async Task<(double X, double Y)?> CenterOf(string sel)
                {
                    var esc = sel.Replace("'", "\\'");
                    var rectJson = await engine.ExecuteJavaScriptAsync(
                        $@"(function() {{ var el = document.querySelector('{esc}'); if (!el) return null; var r = el.getBoundingClientRect(); return {{x: r.x + r.width/2, y: r.y + r.height/2}}; }})()");
                    if (rectJson == "null" || rectJson == "\"null\"") return null;
                    var j = rectJson;
                    if (j.StartsWith("\"")) j = JsonSerializer.Deserialize<string>(j) ?? j;
                    using var doc = JsonDocument.Parse(j);
                    return (doc.RootElement.GetProperty("x").GetDouble(), doc.RootElement.GetProperty("y").GetDouble());
                }

                var src = await CenterOf(selector);
                if (src is null)
                    return await McpResult.ElementNotFound(player, selector);
                var (sx, sy) = (src.Value.X, src.Value.Y);

                double tx, ty;
                if (args?.TryGetProperty("target_selector", out var tgtEl) == true && tgtEl.ValueKind == JsonValueKind.String)
                {
                    var tsel = tgtEl.GetString()!;
                    var tgt = await CenterOf(tsel);
                    if (tgt is null) return await McpResult.ElementNotFound(player, tsel);
                    (tx, ty) = (tgt.Value.X, tgt.Value.Y);
                }
                else
                {
                    tx = args?.TryGetProperty("x", out var xEl) == true ? xEl.GetDouble() : sx;
                    ty = args?.TryGetProperty("y", out var yEl) == true ? yEl.GetDouble() : sy;
                }

                var steps = args?.TryGetProperty("steps", out var stEl) == true ? Math.Clamp(stEl.GetInt32(), 5, 100) : 20;
                var holdMs = args?.TryGetProperty("hold_ms", out var hmEl) == true ? Math.Clamp(hmEl.GetInt32(), 0, 5000) : 120;

                // Position over the source, press (→ pointerdown), hold for delay-based activation
                // constraints, glide to target with the button held (→ pointermove), then release.
                await engine.CallCdpMethodAsync("Input.dispatchMouseEvent",
                    JsonSerializer.Serialize(new { type = "mouseMoved", x = sx, y = sy, button = "none", buttons = 0 }));
                await engine.CallCdpMethodAsync("Input.dispatchMouseEvent",
                    JsonSerializer.Serialize(new { type = "mousePressed", x = sx, y = sy, button = "left", buttons = 1, clickCount = 1 }));
                await Task.Delay(holdMs);

                for (var i = 1; i <= steps; i++)
                {
                    var r = (double)i / steps;
                    var cx = sx + (tx - sx) * r;
                    var cy = sy + (ty - sy) * r;
                    await engine.CallCdpMethodAsync("Input.dispatchMouseEvent",
                        JsonSerializer.Serialize(new { type = "mouseMoved", x = cx, y = cy, button = "left", buttons = 1 }));
                    await Task.Delay(16);
                }

                await Task.Delay(60);
                await engine.CallCdpMethodAsync("Input.dispatchMouseEvent",
                    JsonSerializer.Serialize(new { type = "mouseReleased", x = tx, y = ty, button = "left", buttons = 0, clickCount = 1 }));

                _lastPointer[playerId] = (tx, ty); // cursor ends at the drop point — keep continuity

                return McpResult.Text($"Dragged '{selector}' to ({tx:F0}, {ty:F0}) on player {playerId}");
            });

        registry.Register(
            new McpToolDefinition
            {
                Name = "gdd_scroll",
                Description = "Scroll within a browser player. Provide a CSS selector to scroll that element into view (centered, smooth), or use direction and amount for pixel-based scrolling. Use to reveal off-screen content before reading or tapping.",
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
                },
                Annotations = new { readOnlyHint = false, destructiveHint = false, idempotentHint = false, openWorldHint = false }
            },
            async args =>
            {
                var playerId = args?.GetProperty("player_id").GetInt32() ?? 0;
                var player = await playerManager.GetReadyPlayerAsync(playerId);
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
                Description = "Type text into an input or textarea element identified by CSS selector. Focuses the element and sets its value, firing input and change events. By default clears the field first; set clear=false to append.",
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
                },
                Annotations = new { readOnlyHint = false, destructiveHint = false, idempotentHint = false, openWorldHint = false }
            },
            async args =>
            {
                var playerId = args?.GetProperty("player_id").GetInt32() ?? 0;
                var selector = args?.GetProperty("selector").GetString() ?? "";
                var text = args?.GetProperty("text").GetString() ?? "";
                var clear = true;
                if (args?.TryGetProperty("clear", out var clearEl) == true)
                    clear = clearEl.GetBoolean();

                var player = await playerManager.GetReadyPlayerAsync(playerId);
                if (player?.Engine is null)
                    return McpResult.Error($"Player {playerId} not found or not initialized");

                var escapedSelector = selector.Replace("'", "\\'");
                var escapedText = JsonSerializer.Serialize(text);

                var script = $@"
                    (function() {{
                        var el = document.querySelector('{escapedSelector}');
                        if (!el) return 'not_found';
                        el.focus();
                        var proto = el instanceof HTMLTextAreaElement ? window.HTMLTextAreaElement.prototype
                                  : el instanceof HTMLInputElement ? window.HTMLInputElement.prototype
                                  : null;
                        var setter = proto ? Object.getOwnPropertyDescriptor(proto, 'value').set : null;
                        var newValue = {(clear ? "" : "el.value + ")}{escapedText};
                        if (setter) setter.call(el, newValue);
                        else el.value = newValue;
                        el.dispatchEvent(new Event('input', {{ bubbles: true }}));
                        el.dispatchEvent(new Event('change', {{ bubbles: true }}));
                        return 'ok';
                    }})()";

                var result = await player.Engine.ExecuteJavaScriptAsync(script);
                if (result.Contains("not_found"))
                    return await McpResult.ElementNotFound(player, selector);

                return McpResult.Text($"Typed into '{selector}' on player {playerId}");
            });

        registry.Register(
            new McpToolDefinition
            {
                Name = "gdd_hover",
                Description = "Move the mouse cursor over an element identified by CSS selector, triggering mouseover and mouseenter events. Use to reveal tooltips, dropdown menus, or hover-dependent UI before taking a screenshot or reading content.",
                InputSchema = new
                {
                    type = "object",
                    properties = new
                    {
                        player_id = new { type = "integer", description = "Player ID" },
                        selector = new { type = "string", description = "CSS selector of element to hover" },
                        humanize = new { type = "boolean", description = "Move mouse along a natural curve (0.5-1.5s). Default: false" }
                    },
                    required = new[] { "player_id", "selector" }
                },
                Annotations = new { readOnlyHint = false, destructiveHint = false, idempotentHint = false, openWorldHint = false }
            },
            async args =>
            {
                var playerId = args?.GetProperty("player_id").GetInt32() ?? 0;
                var selector = args?.GetProperty("selector").GetString() ?? "";
                var player = await playerManager.GetReadyPlayerAsync(playerId);
                if (player?.Engine is null)
                    return McpResult.Error($"Player {playerId} not found or not initialized");

                var escapedSelector = selector.Replace("'", "\\'");
                var rectJson = await player.Engine.ExecuteJavaScriptAsync(
                    $@"(function() {{ var el = document.querySelector('{escapedSelector}'); if (!el) return null; var r = el.getBoundingClientRect(); return {{x: r.x + r.width/2, y: r.y + r.height/2}}; }})()");

                if (rectJson == "null" || rectJson == "\"null\"")
                    return await McpResult.ElementNotFound(player, selector);

                var json = rectJson;
                if (json.StartsWith("\""))
                    json = JsonSerializer.Deserialize<string>(json) ?? json;
                using var doc = JsonDocument.Parse(json);
                var x = doc.RootElement.GetProperty("x").GetDouble();
                var y = doc.RootElement.GetProperty("y").GetDouble();

                var humanize = args?.TryGetProperty("humanize", out var hEl) == true && hEl.GetBoolean();

                if (humanize)
                {
                    // Travel from the cursor's last position (continuous path), random start only
                    // on the first move — spanning the real viewport, not the 393x852 default.
                    var start = _lastPointer.TryGetValue(playerId, out var lp)
                        ? lp
                        : MouseMovementService.RandomStart(x, y, player.SelectedDevice.Width, player.SelectedDevice.Height);
                    var path = MouseMovementService.GeneratePath(start.X, start.Y, x, y);
                    foreach (var pt in path)
                    {
                        await player.Engine.CallCdpMethodAsync("Input.dispatchMouseEvent",
                            JsonSerializer.Serialize(new { type = "mouseMoved", x = pt.X, y = pt.Y, button = "none", buttons = 0 }));
                        await Task.Delay(pt.DelayMs);
                    }
                }
                else
                {
                    await player.Engine.CallCdpMethodAsync("Input.dispatchMouseEvent",
                        JsonSerializer.Serialize(new { type = "mouseMoved", x, y, button = "none", buttons = 0 }));
                }

                _lastPointer[playerId] = (x, y); // cursor now rests here — keep continuity for the next move

                return McpResult.Text($"Hovered over '{selector}' at ({x:F0}, {y:F0}) on player {playerId}{(humanize ? " (humanized)" : "")}");
            });

        registry.Register(
            new McpToolDefinition
            {
                Name = "gdd_select",
                Description = "Select an option from a <select> dropdown element. Match by value attribute or visible text. Fires input and change events after selection. Returns the selected option's text. Provide either value or text parameter.",
                InputSchema = new
                {
                    type = "object",
                    properties = new
                    {
                        player_id = new { type = "integer", description = "Player ID" },
                        selector = new { type = "string", description = "CSS selector of <select> element" },
                        value = new { type = "string", description = "Option value to select (matches value attribute)" },
                        text = new { type = "string", description = "Option visible text to select (if value not provided)" }
                    },
                    required = new[] { "player_id", "selector" }
                },
                Annotations = new { readOnlyHint = false, destructiveHint = false, idempotentHint = false, openWorldHint = false }
            },
            async args =>
            {
                var playerId = args?.GetProperty("player_id").GetInt32() ?? 0;
                var selector = args?.GetProperty("selector").GetString() ?? "";
                var player = await playerManager.GetReadyPlayerAsync(playerId);
                if (player?.Engine is null)
                    return McpResult.Error($"Player {playerId} not found or not initialized");

                var escapedSelector = selector.Replace("'", "\\'");
                string matchExpr;
                if (args?.TryGetProperty("value", out var valEl) == true)
                {
                    var val = JsonSerializer.Serialize(valEl.GetString());
                    matchExpr = $"o.value === {val}";
                }
                else if (args?.TryGetProperty("text", out var txtEl) == true)
                {
                    var txt = JsonSerializer.Serialize(txtEl.GetString());
                    matchExpr = $"o.textContent.trim() === {txt}";
                }
                else
                {
                    return McpResult.Error("Either 'value' or 'text' is required");
                }

                var script = $@"(function() {{
                    var sel = document.querySelector('{escapedSelector}');
                    if (!sel) return 'not_found';
                    var opts = sel.options;
                    for (var i = 0; i < opts.length; i++) {{
                        var o = opts[i];
                        if ({matchExpr}) {{
                            sel.value = o.value;
                            sel.dispatchEvent(new Event('input', {{bubbles:true}}));
                            sel.dispatchEvent(new Event('change', {{bubbles:true}}));
                            return o.textContent.trim();
                        }}
                    }}
                    return 'option_not_found';
                }})()";

                var result = await player.Engine.ExecuteJavaScriptAsync(script);
                if (result.Contains("not_found") && !result.Contains("option"))
                    return await McpResult.ElementNotFound(player, selector);
                if (result.Contains("option_not_found"))
                    return McpResult.Error($"Option not found in '{selector}'");

                return McpResult.Text($"Selected '{result.Trim('"')}' in '{selector}' on player {playerId}");
            });

        registry.Register(
            new McpToolDefinition
            {
                Name = "gdd_dialog",
                Description = "Handle a JavaScript dialog (alert, confirm, or prompt). Must be called while a dialog is open. Accept or dismiss the dialog, and optionally enter text for prompt dialogs. Dialogs block page interaction until handled.",
                InputSchema = new
                {
                    type = "object",
                    properties = new
                    {
                        player_id = new { type = "integer", description = "Player ID" },
                        accept = new { type = "boolean", description = "Accept (true) or dismiss (false) the dialog (default true)" },
                        text = new { type = "string", description = "Text to enter in prompt dialog (optional)" }
                    },
                    required = new[] { "player_id" }
                },
                Annotations = new { readOnlyHint = false, destructiveHint = false, idempotentHint = false, openWorldHint = false }
            },
            async args =>
            {
                var playerId = args?.GetProperty("player_id").GetInt32() ?? 0;
                var accept = !(args?.TryGetProperty("accept", out var aEl) == true && !aEl.GetBoolean());
                var promptText = args?.TryGetProperty("text", out var tEl) == true ? tEl.GetString() ?? "" : "";
                var player = await playerManager.GetReadyPlayerAsync(playerId);
                if (player?.Engine is null)
                    return McpResult.Error($"Player {playerId} not found or not initialized");

                await player.Engine.CallCdpMethodAsync("Page.handleJavaScriptDialog",
                    JsonSerializer.Serialize(new { accept, promptText }));

                var action = accept ? "Accepted" : "Dismissed";
                return McpResult.Text($"{action} dialog on player {playerId}");
            });
    }
}
