using System.IO;
using Avalonia.Controls;

namespace GDD.Desktop.Views;

public partial class HelpWindow : Window
{
    public HelpWindow()
    {
        InitializeComponent();
        ManualText.Text = LoadManual();
    }

    private static string LoadManual()
    {
        // GDD-MANUAL.md is copied next to the executable (csproj Content). Fall back to repo root.
        string[] candidates =
        [
            Path.Combine(AppContext.BaseDirectory, "GDD-MANUAL.md"),
            Path.Combine(AppContext.BaseDirectory, "..", "..", "..", "..", "..", "GDD-MANUAL.md")
        ];
        foreach (var path in candidates)
        {
            try
            {
                if (File.Exists(path)) return File.ReadAllText(path);
            }
            catch { /* try next */ }
        }
        return "GDD-MANUAL.md not found next to the executable.";
    }
}
