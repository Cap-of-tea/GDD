using System.Text.Json;
using GDD.Abstractions;

namespace GDD.Services;

/// <summary>
/// Real keyboard input via CDP <c>Input.dispatchKeyEvent</c> — engine-agnostic.
///
/// Everything here produces trusted events (<c>isTrusted === true</c>) and the full
/// per-character chain keydown → keypress → beforeinput → input → keyup, which is what
/// makes input masks, autocomplete, maxlength and contenteditable behave as they do for a
/// real user. The old approach (native value setter + synthetic events) did none of that.
///
/// Verified details, do not "simplify" without re-testing:
/// * A modifier held with a printable key MUST NOT carry <c>text</c>: Ctrl+A sent WITH
///   text types a literal "a" instead of selecting all.
/// * Enter MUST carry <c>text="\r"</c>. Without it Enter is a dead key — no newline in a
///   textarea, no form submit. Tab, by contrast, must carry no text or it inserts one.
/// * Characters with no US-layout key (Cyrillic, accents) still produce the full trusted
///   chain from a plain keyDown carrying only text/unmodifiedText — no degraded mode needed.
/// * Emoji (and other non-BMP chars) cannot be typed as keys; they go through
///   <see cref="InsertTextAsync"/>, which is trusted but fires only beforeinput → input.
/// * <c>change</c> is deliberately never dispatched by hand — the browser fires it on blur,
///   which is the real semantics.
/// </summary>
public static class KeyboardInputService
{
    // CDP modifier bitmask.
    public const int ModAlt = 1;
    public const int ModCtrl = 2;
    public const int ModMeta = 4;
    public const int ModShift = 8;

    /// <param name="Unmodified">What the key yields without Shift ("a" for Shift+A, "1" for Shift+!).</param>
    private sealed record KeyDef(
        string Key, string? Code, int VirtualKeyCode, string? Text, bool Shift, string? Unmodified = null);

    /// <summary>Non-printable keys addressable by name from gdd_press.</summary>
    private static readonly Dictionary<string, KeyDef> NamedKeys = new(StringComparer.OrdinalIgnoreCase)
    {
        // Enter carries "\r" on purpose — see class remarks.
        ["Enter"] = new("Enter", "Enter", 13, "\r", false),
        ["Tab"] = new("Tab", "Tab", 9, null, false),
        ["Backspace"] = new("Backspace", "Backspace", 8, null, false),
        ["Delete"] = new("Delete", "Delete", 46, null, false),
        ["Escape"] = new("Escape", "Escape", 27, null, false),
        ["ArrowLeft"] = new("ArrowLeft", "ArrowLeft", 37, null, false),
        ["ArrowUp"] = new("ArrowUp", "ArrowUp", 38, null, false),
        ["ArrowRight"] = new("ArrowRight", "ArrowRight", 39, null, false),
        ["ArrowDown"] = new("ArrowDown", "ArrowDown", 40, null, false),
        ["Home"] = new("Home", "Home", 36, null, false),
        ["End"] = new("End", "End", 35, null, false),
        ["PageUp"] = new("PageUp", "PageUp", 33, null, false),
        ["PageDown"] = new("PageDown", "PageDown", 34, null, false),
        ["Insert"] = new("Insert", "Insert", 45, null, false),
        ["Space"] = new(" ", "Space", 32, " ", false),
    };

    private static readonly Dictionary<string, int> ModifierNames = new(StringComparer.OrdinalIgnoreCase)
    {
        ["Alt"] = ModAlt,
        ["Control"] = ModCtrl, ["Ctrl"] = ModCtrl,
        ["Meta"] = ModMeta, ["Command"] = ModMeta, ["Cmd"] = ModMeta,
        ["Shift"] = ModShift,
    };

    // Windows virtual-key for the right Alt (AltGr) key.
    private const int VkAltRight = 165;

    /// <summary>
    /// Type one character using the active layout. If the layout resolves the char to a
    /// physical key, we dispatch the exact code/keyCode/modifiers a real keyboard of that
    /// layout reports (incl. Shift, AltGr, and dead-key composition). Characters the layout
    /// can't produce fall back to a layout-agnostic text-only keystroke, which still types
    /// and still fires the full trusted chain.
    /// </summary>
    private static async Task PressCharAsync(IBrowserEngine engine, char c, KeyboardLayout layout)
    {
        var key = layout.Resolve(c);
        if (key is null)
        {
            // Off-layout (other scripts): plain keyDown carrying only text.
            await PressKeyAsync(engine, new KeyDef(c.ToString(), null, 0, c.ToString(), false), 0);
            return;
        }

        var text = c.ToString();
        var unmod = key.Unmodified ?? text;

        // Dead-key composed char (e.g. ^ then e -> ê): first press the dead key, which a real
        // browser reports as key="Dead" with no committed text. Then fall through to press the
        // base letter's physical key carrying the composed character — so the accent lands on
        // its real key (ê on KeyE) with the full trusted chain. (We deliberately don't route
        // this through imeSetComposition: that path emits an untrusted compositionend.)
        if (key.DeadCode is not null)
        {
            await DispatchRawAsync(engine, "rawKeyDown", "Dead", key.DeadCode, key.DeadVk, 0);
            await DispatchRawAsync(engine, "keyUp", "Dead", key.DeadCode, key.DeadVk, 0);
        }

        // AltGr characters (@, €, é on DE/FR): real AltGr is the right-Alt key held, which
        // Windows exposes as Ctrl+Alt. We surround the char with a real AltGraph key press and
        // set Ctrl|Alt on the char. (getModifierState('AltGraph') itself is unreachable via
        // CDP — see stealth-max for the shim that closes that last signal.)
        var altGr = key.AltGr;
        if (altGr)
            await DispatchRawAsync(engine, "rawKeyDown", "AltGraph", "AltRight", VkAltRight, ModAlt, location: 2);

        var mods = (key.Shift ? ModShift : 0) | (altGr ? ModCtrl | ModAlt : 0);

        var down = new Dictionary<string, object>
        {
            ["type"] = "keyDown",
            ["key"] = text,
            ["code"] = key.Code,
            ["windowsVirtualKeyCode"] = key.Vk,
            ["nativeVirtualKeyCode"] = key.Vk,
            ["modifiers"] = mods,
            ["text"] = text,          // carried even under AltGr's Ctrl|Alt so the char is produced
            ["unmodifiedText"] = unmod,
        };
        await engine.CallCdpMethodAsync("Input.dispatchKeyEvent", JsonSerializer.Serialize(down));
        await engine.CallCdpMethodAsync("Input.dispatchKeyEvent", JsonSerializer.Serialize(new
        {
            type = "keyUp",
            key = text,
            code = key.Code,
            windowsVirtualKeyCode = key.Vk,
            nativeVirtualKeyCode = key.Vk,
            modifiers = mods,
        }));

        if (altGr)
            await DispatchRawAsync(engine, "keyUp", "AltGraph", "AltRight", VkAltRight, 0, location: 2);
    }

    private static Task DispatchRawAsync(
        IBrowserEngine engine, string type, string key, string code, int vk, int modifiers, int? location = null)
    {
        var p = new Dictionary<string, object>
        {
            ["type"] = type, ["key"] = key, ["code"] = code,
            ["windowsVirtualKeyCode"] = vk, ["nativeVirtualKeyCode"] = vk, ["modifiers"] = modifiers,
        };
        if (location is not null) p["location"] = location.Value;
        return engine.CallCdpMethodAsync("Input.dispatchKeyEvent", JsonSerializer.Serialize(p));
    }

    private static async Task DispatchAsync(IBrowserEngine engine, string type, KeyDef def, int modifiers)
    {
        // A printable key combined with Ctrl/Alt/Meta must not carry text, or the character
        // gets inserted instead of the shortcut firing.
        var suppressText = (modifiers & (ModCtrl | ModAlt | ModMeta)) != 0;
        var wantsText = def.Text is not null && !suppressText && type != "keyUp";

        var payload = new Dictionary<string, object>
        {
            ["type"] = wantsText ? "keyDown" : type == "keyDown" ? "rawKeyDown" : type,
            ["key"] = def.Key,
            ["modifiers"] = modifiers,
        };

        if (def.Code is not null)
        {
            payload["code"] = def.Code;
            payload["windowsVirtualKeyCode"] = def.VirtualKeyCode;
            payload["nativeVirtualKeyCode"] = def.VirtualKeyCode;
        }

        if (wantsText)
        {
            payload["text"] = def.Text!;
            payload["unmodifiedText"] = def.Unmodified ?? def.Text!;
        }

        await engine.CallCdpMethodAsync("Input.dispatchKeyEvent", JsonSerializer.Serialize(payload));
    }

    private static async Task PressKeyAsync(IBrowserEngine engine, KeyDef def, int modifiers)
    {
        var mods = modifiers | (def.Shift ? ModShift : 0);
        await DispatchAsync(engine, "keyDown", def, mods);
        await DispatchAsync(engine, "keyUp", def, mods);
    }

    /// <summary>
    /// Type text as real keystrokes. Newlines are typed as Enter. Characters that cannot be
    /// expressed as keys at all (emoji and other surrogate pairs) fall back to insertText.
    /// </summary>
    /// <param name="humanize">Add per-character jitter (~40-120ms) instead of typing flat out.</param>
    /// <param name="delayMs">Fixed per-character delay; ignored when humanize is set.</param>
    /// <param name="layout">Keyboard layout for physical code/keyCode fidelity (default US QWERTY).</param>
    public static async Task TypeAsync(
        IBrowserEngine engine, string text, bool humanize = false, int delayMs = 0, KeyboardLayout? layout = null)
    {
        layout ??= KeyboardLayout.Us;
        var rng = Random.Shared;

        for (int i = 0; i < text.Length; i++)
        {
            var c = text[i];

            if (char.IsSurrogate(c))
            {
                // Non-BMP (emoji): not typeable as a key — insert the whole pair verbatim.
                var len = char.IsHighSurrogate(c) && i + 1 < text.Length ? 2 : 1;
                await InsertTextAsync(engine, text.Substring(i, len));
                i += len - 1;
            }
            else if (c is '\n' or '\r')
            {
                // Treat CRLF as one Enter.
                if (c == '\r' && i + 1 < text.Length && text[i + 1] == '\n') i++;
                await PressKeyAsync(engine, NamedKeys["Enter"], 0);
            }
            else if (c == '\t')
            {
                await PressKeyAsync(engine, NamedKeys["Tab"], 0);
            }
            else
            {
                await PressCharAsync(engine, c, layout);
            }

            if (humanize)
                await Task.Delay(rng.Next(40, 121));
            else if (delayMs > 0)
                await Task.Delay(delayMs);
        }
    }

    /// <summary>
    /// Press a single named key or character, optionally with modifiers
    /// (Alt / Control / Meta / Shift).
    /// </summary>
    /// <returns>false if the key name is not recognised.</returns>
    public static async Task<bool> PressAsync(
        IBrowserEngine engine, string key, IReadOnlyList<string>? modifiers = null, int count = 1,
        KeyboardLayout? layout = null)
    {
        layout ??= KeyboardLayout.Us;
        var mods = 0;
        foreach (var m in modifiers ?? [])
        {
            if (!ModifierNames.TryGetValue(m, out var bit)) return false;
            mods |= bit;
        }

        // A single character with no explicit modifiers goes through the layout, so a bare
        // gdd_press("а") on a Russian layout still reports KeyF/70.
        if (mods == 0 && count >= 1 && key.Length == 1 && !NamedKeys.ContainsKey(key))
        {
            for (int i = 0; i < Math.Max(1, count); i++)
                await PressCharAsync(engine, key[0], layout);
            return true;
        }

        KeyDef def;
        if (NamedKeys.TryGetValue(key, out var named)) def = named;
        else if (key.Length == 1)
        {
            var s = key;
            // With modifiers (a shortcut) the character rides US positions.
            var resolved = KeyboardLayout.Us.Resolve(key[0]);
            def = resolved is null
                ? new KeyDef(s, null, 0, s, false)
                : new KeyDef(s, resolved.Code, resolved.Vk, s, resolved.Shift, resolved.Unmodified);
        }
        else if (key.Length is 2 or 3 && (key[0] is 'F' or 'f') && int.TryParse(key[1..], out var fn) && fn is >= 1 and <= 12)
            def = new KeyDef($"F{fn}", $"F{fn}", 111 + fn, null, false);
        else return false;

        for (int i = 0; i < Math.Max(1, count); i++)
            await PressKeyAsync(engine, def, mods);

        return true;
    }

    /// <summary>
    /// Clear the focused field with real keys (select-all then Delete). Works for inputs,
    /// textareas and contenteditable alike. The explicit selectAll command is what makes
    /// this correct on macOS, where the shortcut is Cmd+A rather than Ctrl+A.
    /// </summary>
    public static async Task ClearAsync(IBrowserEngine engine)
    {
        var isMac = OperatingSystem.IsMacOS();
        var mods = isMac ? ModMeta : ModCtrl;

        await engine.CallCdpMethodAsync("Input.dispatchKeyEvent", JsonSerializer.Serialize(new
        {
            type = "rawKeyDown",
            key = "a",
            code = "KeyA",
            windowsVirtualKeyCode = 65,
            nativeVirtualKeyCode = 65,
            modifiers = mods,
            commands = new[] { "selectAll" },
        }));
        await engine.CallCdpMethodAsync("Input.dispatchKeyEvent", JsonSerializer.Serialize(new
        {
            type = "keyUp",
            key = "a",
            code = "KeyA",
            windowsVirtualKeyCode = 65,
            nativeVirtualKeyCode = 65,
            modifiers = mods,
        }));

        await PressKeyAsync(engine, NamedKeys["Delete"], 0);
    }

    /// <summary>
    /// Insert text in one shot via CDP <c>Input.insertText</c> — the paste hatch. Trusted,
    /// but fires only beforeinput → input with no key events, which is honest paste
    /// semantics (and the only way to enter emoji).
    /// </summary>
    public static Task InsertTextAsync(IBrowserEngine engine, string text) =>
        engine.CallCdpMethodAsync("Input.insertText", JsonSerializer.Serialize(new { text }));
}
