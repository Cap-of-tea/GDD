using Avalonia.Data.Converters;

namespace GDD.Desktop.Converters;

public static class Converters
{
    /// <summary>int &gt; 0 → true (for badge visibility).</summary>
    public static readonly IValueConverter PositiveToVisible =
        new FuncValueConverter<int, bool>(i => i > 0);
}
