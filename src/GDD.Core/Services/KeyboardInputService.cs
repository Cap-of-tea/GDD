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

    // US-layout punctuation: char -> (code, virtual key, needs shift, unshifted char).
    private static readonly Dictionary<char, (string Code, int Vk, bool Shift, string Unmodified)> Punctuation = new()
    {
        ['`'] = ("Backquote", 192, false, "`"), ['~'] = ("Backquote", 192, true, "`"),
        ['-'] = ("Minus", 189, false, "-"), ['_'] = ("Minus", 189, true, "-"),
        ['='] = ("Equal", 187, false, "="), ['+'] = ("Equal", 187, true, "="),
        ['['] = ("BracketLeft", 219, false, "["), ['{'] = ("BracketLeft", 219, true, "["),
        [']'] = ("BracketRight", 221, false, "]"), ['}'] = ("BracketRight", 221, true, "]"),
        ['\\'] = ("Backslash", 220, false, "\\"), ['|'] = ("Backslash", 220, true, "\\"),
        [';'] = ("Semicolon", 186, false, ";"), [':'] = ("Semicolon", 186, true, ";"),
        ['\''] = ("Quote", 222, false, "'"), ['"'] = ("Quote", 222, true, "'"),
        [','] = ("Comma", 188, false, ","), ['<'] = ("Comma", 188, true, ","),
        ['.'] = ("Period", 190, false, "."), ['>'] = ("Period", 190, true, "."),
        ['/'] = ("Slash", 191, false, "/"), ['?'] = ("Slash", 191, true, "/"),
        ['!'] = ("Digit1", 49, true, "1"), ['@'] = ("Digit2", 50, true, "2"),
        ['#'] = ("Digit3", 51, true, "3"), ['$'] = ("Digit4", 52, true, "4"),
        ['%'] = ("Digit5", 53, true, "5"), ['^'] = ("Digit6", 54, true, "6"),
        ['&'] = ("Digit7", 55, true, "7"), ['*'] = ("Digit8", 56, true, "8"),
        ['('] = ("Digit9", 57, true, "9"), [')'] = ("Digit0", 48, true, "0"),
        [' '] = ("Space", 32, false, " "),
    };

    /// <summary>
    /// Map one character to a US-layout key. Characters outside the layout (Cyrillic,
    /// accents, CJK) return a text-only definition, which still types correctly.
    /// </summary>
    private static KeyDef MapChar(char c)
    {
        var s = c.ToString();

        if (c is >= 'a' and <= 'z')
            return new KeyDef(s, $"Key{char.ToUpperInvariant(c)}", char.ToUpperInvariant(c), s, false, s);
        if (c is >= 'A' and <= 'Z')
            return new KeyDef(s, $"Key{c}", c, s, true, char.ToLowerInvariant(c).ToString());
        if (c is >= '0' and <= '9')
            return new KeyDef(s, $"Digit{c}", c, s, false, s);
        if (Punctuation.TryGetValue(c, out var p))
            return new KeyDef(s, p.Code, p.Vk, s, p.Shift, p.Unmodified);

        // Off-layout: no code/keycode, but a plain keyDown with text still fires the
        // complete trusted chain.
        return new KeyDef(s, null, 0, s, false);
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
    public static async Task TypeAsync(
        IBrowserEngine engine, string text, bool humanize = false, int delayMs = 0)
    {
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
                await PressKeyAsync(engine, MapChar(c), 0);
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
        IBrowserEngine engine, string key, IReadOnlyList<string>? modifiers = null, int count = 1)
    {
        var mods = 0;
        foreach (var m in modifiers ?? [])
        {
            if (!ModifierNames.TryGetValue(m, out var bit)) return false;
            mods |= bit;
        }

        KeyDef def;
        if (NamedKeys.TryGetValue(key, out var named)) def = named;
        else if (key.Length == 1) def = MapChar(key[0]);
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
