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

/// <summary>
/// German QWERTZ (T1). Y/Z are swapped from US, umlauts and ß sit on dedicated keys, and the
/// common programming symbols (@ € { } [ ] \ ~ | µ) are on AltGr. Accents (é, ê) come from the
/// dead ´/`/^ keys. Latin letters other than y/z, and the digit row (unshifted digits), share
/// US positions.
/// </summary>
internal sealed class DeLayout : KeyboardLayout
{
    public override string Id => "de";

    public override LayoutKey? Resolve(char c)
    {
        // QWERTZ swap: physical Y key yields z, physical Z key yields y.
        switch (c)
        {
            case 'z': return new LayoutKey("KeyY", 89, Unmodified: "z");
            case 'Z': return new LayoutKey("KeyY", 89, Shift: true, Unmodified: "z");
            case 'y': return new LayoutKey("KeyZ", 90, Unmodified: "y");
            case 'Y': return new LayoutKey("KeyZ", 90, Shift: true, Unmodified: "y");
        }
        if (Map.TryGetValue(c, out var k)) return k;
        // Other Latin letters and unshifted digits are in the same place as US.
        if ((c is >= 'a' and <= 'x') || (c is >= 'A' and <= 'X') || (c is >= '0' and <= '9'))
            return UsLetter(c) ?? UsDigit(c);
        return null;
    }

    private static readonly Dictionary<char, LayoutKey> Map = new()
    {
        // umlauts + ß
        ['ü'] = new("BracketLeft", 219, Unmodified: "ü"), ['Ü'] = new("BracketLeft", 219, Shift: true, Unmodified: "ü"),
        ['ö'] = new("Semicolon", 186, Unmodified: "ö"), ['Ö'] = new("Semicolon", 186, Shift: true, Unmodified: "ö"),
        ['ä'] = new("Quote", 222, Unmodified: "ä"), ['Ä'] = new("Quote", 222, Shift: true, Unmodified: "ä"),
        ['ß'] = new("Minus", 189, Unmodified: "ß"),
        // number-row shifted symbols: ! " § $ % & / ( ) = ?
        ['!'] = new("Digit1", 49, Shift: true, Unmodified: "1"), ['"'] = new("Digit2", 50, Shift: true, Unmodified: "2"),
        ['§'] = new("Digit3", 51, Shift: true, Unmodified: "3"), ['$'] = new("Digit4", 52, Shift: true, Unmodified: "4"),
        ['%'] = new("Digit5", 53, Shift: true, Unmodified: "5"), ['&'] = new("Digit6", 54, Shift: true, Unmodified: "6"),
        ['/'] = new("Digit7", 55, Shift: true, Unmodified: "7"), ['('] = new("Digit8", 56, Shift: true, Unmodified: "8"),
        [')'] = new("Digit9", 57, Shift: true, Unmodified: "9"), ['='] = new("Digit0", 48, Shift: true, Unmodified: "0"),
        ['?'] = new("Minus", 189, Shift: true, Unmodified: "ß"),
        // other punctuation
        [','] = new("Comma", 188, Unmodified: ","), [';'] = new("Comma", 188, Shift: true, Unmodified: ","),
        ['.'] = new("Period", 190, Unmodified: "."), [':'] = new("Period", 190, Shift: true, Unmodified: "."),
        ['-'] = new("Slash", 191, Unmodified: "-"), ['_'] = new("Slash", 191, Shift: true, Unmodified: "-"),
        ['#'] = new("Backslash", 220, Unmodified: "#"), ['\''] = new("Backslash", 220, Shift: true, Unmodified: "#"),
        ['+'] = new("BracketRight", 221, Unmodified: "+"), ['*'] = new("BracketRight", 221, Shift: true, Unmodified: "+"),
        ['<'] = new("IntlBackslash", 226, Unmodified: "<"), ['>'] = new("IntlBackslash", 226, Shift: true, Unmodified: "<"),
        [' '] = new("Space", 32, Unmodified: " "),
        // AltGr symbols
        ['@'] = new("KeyQ", 81, AltGr: true, Unmodified: "q"), ['€'] = new("KeyE", 69, AltGr: true, Unmodified: "e"),
        ['µ'] = new("KeyM", 77, AltGr: true, Unmodified: "m"),
        ['²'] = new("Digit2", 50, AltGr: true, Unmodified: "2"), ['³'] = new("Digit3", 51, AltGr: true, Unmodified: "3"),
        ['{'] = new("Digit7", 55, AltGr: true, Unmodified: "7"), ['['] = new("Digit8", 56, AltGr: true, Unmodified: "8"),
        [']'] = new("Digit9", 57, AltGr: true, Unmodified: "9"), ['}'] = new("Digit0", 48, AltGr: true, Unmodified: "0"),
        ['\\'] = new("Minus", 189, AltGr: true, Unmodified: "ß"), ['~'] = new("BracketRight", 221, AltGr: true, Unmodified: "+"),
        ['|'] = new("IntlBackslash", 226, AltGr: true, Unmodified: "<"),
        // dead-key accents (´ = acute on Equal, ` = grave on Equal+Shift, ^ = circumflex on Backquote)
        ['é'] = new("KeyE", 69, DeadCode: "Equal", DeadVk: 187, DeadKey: "´"),
        ['á'] = new("KeyA", 65, DeadCode: "Equal", DeadVk: 187, DeadKey: "´"),
        ['ó'] = new("KeyO", 79, DeadCode: "Equal", DeadVk: 187, DeadKey: "´"),
        ['ú'] = new("KeyU", 85, DeadCode: "Equal", DeadVk: 187, DeadKey: "´"),
        ['è'] = new("KeyE", 69, DeadCode: "Equal", DeadVk: 187, DeadKey: "`"),
        ['à'] = new("KeyA", 65, DeadCode: "Equal", DeadVk: 187, DeadKey: "`"),
        ['ù'] = new("KeyU", 85, DeadCode: "Equal", DeadVk: 187, DeadKey: "`"),
        ['ê'] = new("KeyE", 69, DeadCode: "Backquote", DeadVk: 192, DeadKey: "^"),
        ['â'] = new("KeyA", 65, DeadCode: "Backquote", DeadVk: 192, DeadKey: "^"),
        ['î'] = new("KeyI", 73, DeadCode: "Backquote", DeadVk: 192, DeadKey: "^"),
        ['ô'] = new("KeyO", 79, DeadCode: "Backquote", DeadVk: 192, DeadKey: "^"),
        ['û'] = new("KeyU", 85, DeadCode: "Backquote", DeadVk: 192, DeadKey: "^"),
    };
}

/// <summary>
/// French AZERTY. A/Q and Z/W are swapped, M sits where US has the semicolon, the number row
/// is symbols unshifted (digits require Shift), the accented letters é è ç à ù are direct keys,
/// and programming symbols (@ # { } [ ] etc.) are on AltGr. Circumflex/diaeresis are dead keys.
/// </summary>
internal sealed class FrLayout : KeyboardLayout
{
    public override string Id => "fr";

    public override LayoutKey? Resolve(char c)
    {
        // AZERTY letter relocations.
        switch (c)
        {
            case 'a': return new LayoutKey("KeyQ", 81, Unmodified: "a");
            case 'A': return new LayoutKey("KeyQ", 81, Shift: true, Unmodified: "a");
            case 'q': return new LayoutKey("KeyA", 65, Unmodified: "q");
            case 'Q': return new LayoutKey("KeyA", 65, Shift: true, Unmodified: "q");
            case 'z': return new LayoutKey("KeyW", 87, Unmodified: "z");
            case 'Z': return new LayoutKey("KeyW", 87, Shift: true, Unmodified: "z");
            case 'w': return new LayoutKey("KeyZ", 90, Unmodified: "w");
            case 'W': return new LayoutKey("KeyZ", 90, Shift: true, Unmodified: "w");
            case 'm': return new LayoutKey("Semicolon", 186, Unmodified: "m");
            case 'M': return new LayoutKey("Semicolon", 186, Shift: true, Unmodified: "m");
        }
        if (Map.TryGetValue(c, out var k)) return k;
        // Remaining Latin letters keep US positions.
        if ((c is >= 'b' and <= 'y') || (c is >= 'B' and <= 'Y')) return UsLetter(c);
        return null;
    }

    private static readonly Dictionary<char, LayoutKey> Map = new()
    {
        // number row unshifted: & é " ' ( - è _ ç à ) =
        ['&'] = new("Digit1", 49, Unmodified: "&"), ['é'] = new("Digit2", 50, Unmodified: "é"),
        ['"'] = new("Digit3", 51, Unmodified: "\""), ['\''] = new("Digit4", 52, Unmodified: "'"),
        ['('] = new("Digit5", 53, Unmodified: "("), ['-'] = new("Digit6", 54, Unmodified: "-"),
        ['è'] = new("Digit7", 55, Unmodified: "è"), ['_'] = new("Digit8", 56, Unmodified: "_"),
        ['ç'] = new("Digit9", 57, Unmodified: "ç"), ['à'] = new("Digit0", 48, Unmodified: "à"),
        [')'] = new("Minus", 189, Unmodified: ")"), ['='] = new("Equal", 187, Unmodified: "="),
        // digits require Shift
        ['1'] = new("Digit1", 49, Shift: true, Unmodified: "&"), ['2'] = new("Digit2", 50, Shift: true, Unmodified: "é"),
        ['3'] = new("Digit3", 51, Shift: true, Unmodified: "\""), ['4'] = new("Digit4", 52, Shift: true, Unmodified: "'"),
        ['5'] = new("Digit5", 53, Shift: true, Unmodified: "("), ['6'] = new("Digit6", 54, Shift: true, Unmodified: "-"),
        ['7'] = new("Digit7", 55, Shift: true, Unmodified: "è"), ['8'] = new("Digit8", 56, Shift: true, Unmodified: "_"),
        ['9'] = new("Digit9", 57, Shift: true, Unmodified: "ç"), ['0'] = new("Digit0", 48, Shift: true, Unmodified: "à"),
        ['°'] = new("Minus", 189, Shift: true, Unmodified: ")"), ['+'] = new("Equal", 187, Shift: true, Unmodified: "="),
        // home/bottom punctuation
        ['ù'] = new("Quote", 222, Unmodified: "ù"), ['%'] = new("Quote", 222, Shift: true, Unmodified: "ù"),
        ['*'] = new("Backslash", 220, Unmodified: "*"), ['µ'] = new("Backslash", 220, Shift: true, Unmodified: "*"),
        [','] = new("KeyM", 77, Unmodified: ","), ['?'] = new("KeyM", 77, Shift: true, Unmodified: ","),
        [';'] = new("Comma", 188, Unmodified: ";"), ['.'] = new("Comma", 188, Shift: true, Unmodified: ";"),
        [':'] = new("Period", 190, Unmodified: ":"), ['/'] = new("Period", 190, Shift: true, Unmodified: ":"),
        ['!'] = new("Slash", 191, Unmodified: "!"), ['§'] = new("Slash", 191, Shift: true, Unmodified: "!"),
        ['$'] = new("BracketRight", 221, Unmodified: "$"), ['£'] = new("BracketRight", 221, Shift: true, Unmodified: "$"),
        ['<'] = new("IntlBackslash", 226, Unmodified: "<"), ['>'] = new("IntlBackslash", 226, Shift: true, Unmodified: "<"),
        [' '] = new("Space", 32, Unmodified: " "),
        // AltGr symbols
        ['~'] = new("Digit2", 50, AltGr: true, Unmodified: "é"), ['#'] = new("Digit3", 51, AltGr: true, Unmodified: "\""),
        ['{'] = new("Digit4", 52, AltGr: true, Unmodified: "'"), ['['] = new("Digit5", 53, AltGr: true, Unmodified: "("),
        ['|'] = new("Digit6", 54, AltGr: true, Unmodified: "-"), ['`'] = new("Digit7", 55, AltGr: true, Unmodified: "è"),
        ['\\'] = new("Digit8", 56, AltGr: true, Unmodified: "_"), ['^'] = new("Digit9", 57, AltGr: true, Unmodified: "ç"),
        ['@'] = new("Digit0", 48, AltGr: true, Unmodified: "à"), [']'] = new("Minus", 189, AltGr: true, Unmodified: ")"),
        ['}'] = new("Equal", 187, AltGr: true, Unmodified: "="), ['€'] = new("KeyE", 69, AltGr: true, Unmodified: "e"),
        // dead-key accents: ^ (circumflex) and ¨ (diaeresis) both on the key right of P
        ['ê'] = new("KeyE", 69, DeadCode: "BracketLeft", DeadVk: 219, DeadKey: "^"),
        ['â'] = new("KeyQ", 81, DeadCode: "BracketLeft", DeadVk: 219, DeadKey: "^"),
        ['î'] = new("KeyI", 73, DeadCode: "BracketLeft", DeadVk: 219, DeadKey: "^"),
        ['ô'] = new("KeyO", 79, DeadCode: "BracketLeft", DeadVk: 219, DeadKey: "^"),
        ['û'] = new("KeyU", 85, DeadCode: "BracketLeft", DeadVk: 219, DeadKey: "^"),
        ['ë'] = new("KeyE", 69, DeadCode: "BracketLeft", DeadVk: 219, DeadKey: "¨"),
        ['ï'] = new("KeyI", 73, DeadCode: "BracketLeft", DeadVk: 219, DeadKey: "¨"),
        ['ü'] = new("KeyU", 85, DeadCode: "BracketLeft", DeadVk: 219, DeadKey: "¨"),
    };
}
