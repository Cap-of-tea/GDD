namespace GDD.Services;

/// <summary>
/// A physical keystroke that produces one character on a specific keyboard layout.
/// <c>Code</c> is the DOM <c>KeyboardEvent.code</c> (the physical key, layout-independent);
/// <c>Vk</c> is the Windows virtual-key code that Chromium surfaces as legacy
/// <c>keyCode</c>. <c>Shift</c>/<c>AltGr</c> are the modifiers a real keyboard would hold to
/// produce the character. <c>DeadCode</c>/<c>DeadVk</c>, when set, mean the character is
/// produced by first pressing a dead key (e.g. circumflex) and then composing.
/// </summary>
public sealed record LayoutKey(
    string Code,
    int Vk,
    bool Shift = false,
    bool AltGr = false,
    string? Unmodified = null,
    string? DeadCode = null,
    int DeadVk = 0,
    string? DeadKey = null);

/// <summary>
/// Maps a character to the physical keystroke a real keyboard of a given layout would use.
/// This is what makes <c>event.code</c>/<c>event.keyCode</c> match a genuine keyboard for the
/// emulated locale (a Russian ЙЦУКЕН keyboard reports «а» on <c>KeyF</c>/70, not text-only).
/// A layout returns <c>null</c> for characters it cannot produce directly; the caller then
/// falls back to a layout-agnostic text-only keystroke.
/// </summary>
public abstract class KeyboardLayout
{
    /// <summary>Short id: <c>us</c>, <c>ru</c>, <c>de</c>, <c>fr</c>.</summary>
    public abstract string Id { get; }

    /// <summary>Resolve a printable character, or null if this layout can't produce it directly.</summary>
    public abstract LayoutKey? Resolve(char c);

    public static readonly KeyboardLayout Us = new UsLayout();
    public static readonly KeyboardLayout Ru = new RuLayout();
    public static readonly KeyboardLayout De = new DeLayout();
    public static readonly KeyboardLayout Fr = new FrLayout();

    /// <summary>Resolve an explicit layout id/name; unknown → US.</summary>
    public static KeyboardLayout FromId(string? id) => (id ?? "").Trim().ToLowerInvariant() switch
    {
        "ru" or "rus" or "ru-ru" or "russian" or "jcuken" or "йцукен" => Ru,
        "de" or "de-de" or "german" or "qwertz" => De,
        "fr" or "fr-fr" or "french" or "azerty" => Fr,
        _ => Us,
    };

    /// <summary>Pick the layout that matches an emulated locale (e.g. <c>ru-RU</c> → ЙЦУКЕН).</summary>
    public static KeyboardLayout FromLocale(string? locale)
    {
        var l = (locale ?? "").Trim().ToLowerInvariant();
        if (l.StartsWith("ru")) return Ru;
        if (l.StartsWith("de")) return De;
        if (l.StartsWith("fr")) return Fr;
        return Us;
    }

    // ---- shared US-QWERTY primitives, reused by other layouts ----

    private protected static LayoutKey? UsLetter(char c)
    {
        if (c is >= 'a' and <= 'z')
        {
            var up = char.ToUpperInvariant(c);
            return new LayoutKey($"Key{up}", up, Unmodified: c.ToString());
        }
        if (c is >= 'A' and <= 'Z')
            return new LayoutKey($"Key{c}", c, Shift: true, Unmodified: char.ToLowerInvariant(c).ToString());
        return null;
    }

    private protected static LayoutKey? UsDigit(char c) =>
        c is >= '0' and <= '9' ? new LayoutKey($"Digit{c}", c, Unmodified: c.ToString()) : null;
}

/// <summary>US QWERTY — the default and the baseline all other layouts share for basic keys.</summary>
internal sealed class UsLayout : KeyboardLayout
{
    public override string Id => "us";

    public override LayoutKey? Resolve(char c) =>
        UsLetter(c) ?? UsDigit(c) ?? (Punct.TryGetValue(c, out var k) ? k : null);

    // char -> (code, vk, shift, unshifted char)
    internal static readonly Dictionary<char, LayoutKey> Punct = new()
    {
        ['`'] = new("Backquote", 192, Unmodified: "`"), ['~'] = new("Backquote", 192, Shift: true, Unmodified: "`"),
        ['-'] = new("Minus", 189, Unmodified: "-"), ['_'] = new("Minus", 189, Shift: true, Unmodified: "-"),
        ['='] = new("Equal", 187, Unmodified: "="), ['+'] = new("Equal", 187, Shift: true, Unmodified: "="),
        ['['] = new("BracketLeft", 219, Unmodified: "["), ['{'] = new("BracketLeft", 219, Shift: true, Unmodified: "["),
        [']'] = new("BracketRight", 221, Unmodified: "]"), ['}'] = new("BracketRight", 221, Shift: true, Unmodified: "]"),
        ['\\'] = new("Backslash", 220, Unmodified: "\\"), ['|'] = new("Backslash", 220, Shift: true, Unmodified: "\\"),
        [';'] = new("Semicolon", 186, Unmodified: ";"), [':'] = new("Semicolon", 186, Shift: true, Unmodified: ";"),
        ['\''] = new("Quote", 222, Unmodified: "'"), ['"'] = new("Quote", 222, Shift: true, Unmodified: "'"),
        [','] = new("Comma", 188, Unmodified: ","), ['<'] = new("Comma", 188, Shift: true, Unmodified: ","),
        ['.'] = new("Period", 190, Unmodified: "."), ['>'] = new("Period", 190, Shift: true, Unmodified: "."),
        ['/'] = new("Slash", 191, Unmodified: "/"), ['?'] = new("Slash", 191, Shift: true, Unmodified: "/"),
        ['!'] = new("Digit1", 49, Shift: true, Unmodified: "1"), ['@'] = new("Digit2", 50, Shift: true, Unmodified: "2"),
        ['#'] = new("Digit3", 51, Shift: true, Unmodified: "3"), ['$'] = new("Digit4", 52, Shift: true, Unmodified: "4"),
        ['%'] = new("Digit5", 53, Shift: true, Unmodified: "5"), ['^'] = new("Digit6", 54, Shift: true, Unmodified: "6"),
        ['&'] = new("Digit7", 55, Shift: true, Unmodified: "7"), ['*'] = new("Digit8", 56, Shift: true, Unmodified: "8"),
        ['('] = new("Digit9", 57, Shift: true, Unmodified: "9"), [')'] = new("Digit0", 48, Shift: true, Unmodified: "0"),
        [' '] = new("Space", 32, Unmodified: " "),
    };
}

/// <summary>
/// Russian ЙЦУКЕН. Cyrillic letters sit on fixed physical QWERTY keys — «а» on the physical
/// F key (<c>KeyF</c>/70), «в» on D, «ы» on S, and so on — which is exactly what a real
/// Russian keyboard reports. Latin letters and shared symbols fall through to US, modelling a
/// bilingual user who switches layouts for URLs/emails.
/// </summary>
internal sealed class RuLayout : KeyboardLayout
{
    public override string Id => "ru";

    public override LayoutKey? Resolve(char c)
    {
        if (Map.TryGetValue(char.ToLowerInvariant(c), out var k))
        {
            var shift = char.IsUpper(c);
            var lower = char.ToLowerInvariant(c).ToString();
            return k with { Shift = k.Shift || shift, Unmodified = k.Unmodified ?? lower };
        }
        if (ShiftPunct.TryGetValue(c, out var p)) return p;
        // Digits, Latin and common punctuation: a bilingual user switches to US for these.
        return Us.Resolve(c);
    }

    // Cyrillic lowercase -> physical QWERTY key (code, vk). Case handled in Resolve.
    private static readonly Dictionary<char, LayoutKey> Map = new()
    {
        ['ё'] = new("Backquote", 192),
        ['й'] = new("KeyQ", 81), ['ц'] = new("KeyW", 87), ['у'] = new("KeyE", 69), ['к'] = new("KeyR", 82),
        ['е'] = new("KeyT", 84), ['н'] = new("KeyY", 89), ['г'] = new("KeyU", 85), ['ш'] = new("KeyI", 73),
        ['щ'] = new("KeyO", 79), ['з'] = new("KeyP", 80), ['х'] = new("BracketLeft", 219), ['ъ'] = new("BracketRight", 221),
        ['ф'] = new("KeyA", 65), ['ы'] = new("KeyS", 83), ['в'] = new("KeyD", 68), ['а'] = new("KeyF", 70),
        ['п'] = new("KeyG", 71), ['р'] = new("KeyH", 72), ['о'] = new("KeyJ", 74), ['л'] = new("KeyK", 75),
        ['д'] = new("KeyL", 76), ['ж'] = new("Semicolon", 186), ['э'] = new("Quote", 222),
        ['я'] = new("KeyZ", 90), ['ч'] = new("KeyX", 88), ['с'] = new("KeyC", 67), ['м'] = new("KeyV", 86),
        ['и'] = new("KeyB", 66), ['т'] = new("KeyN", 78), ['ь'] = new("KeyM", 77), ['б'] = new("Comma", 188),
        ['ю'] = new("Period", 190),
    };

    // Russian-layout punctuation reached via Shift on the number row, plus the Slash key.
    private static readonly Dictionary<char, LayoutKey> ShiftPunct = new()
    {
        ['!'] = new("Digit1", 49, Shift: true, Unmodified: "1"),
        ['"'] = new("Digit2", 50, Shift: true, Unmodified: "2"),
        ['№'] = new("Digit3", 51, Shift: true, Unmodified: "3"),
        [';'] = new("Digit4", 52, Shift: true, Unmodified: "4"),
        ['%'] = new("Digit5", 53, Shift: true, Unmodified: "5"),
        [':'] = new("Digit6", 54, Shift: true, Unmodified: "6"),
        ['?'] = new("Digit7", 55, Shift: true, Unmodified: "7"),
        ['*'] = new("Digit8", 56, Shift: true, Unmodified: "8"),
        ['('] = new("Digit9", 57, Shift: true, Unmodified: "9"),
        [')'] = new("Digit0", 48, Shift: true, Unmodified: "0"),
        ['.'] = new("Slash", 191, Unmodified: "."),
        [','] = new("Slash", 191, Shift: true, Unmodified: "."),
    };
}

// DE/FR are filled in the next stage; stubbed to US so the model compiles and the default
// path is unaffected until their tables are built and validated live.
internal sealed class DeLayout : KeyboardLayout
{
    public override string Id => "de";
    public override LayoutKey? Resolve(char c) => Us.Resolve(c);
}

internal sealed class FrLayout : KeyboardLayout
{
    public override string Id => "fr";
    public override LayoutKey? Resolve(char c) => Us.Resolve(c);
}
