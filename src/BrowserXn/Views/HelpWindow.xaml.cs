using System.IO;
using System.Windows;
using GDD.Interop;

namespace GDD.Views;

public partial class HelpWindow : Window
{
    public HelpWindow()
    {
        InitializeComponent();
        SourceInitialized += (_, _) => DarkTitleBar.Apply(this);
        LoadManual();
    }

    private void LoadManual()
    {
        var manualPath = Path.Combine(AppContext.BaseDirectory, "GDD-MANUAL.md");
        if (!File.Exists(manualPath))
            manualPath = Path.Combine(AppDomain.CurrentDomain.BaseDirectory, "..", "..", "..", "..", "..", "GDD-MANUAL.md");

        if (File.Exists(manualPath))
            ManualText.Text = File.ReadAllText(manualPath);
        else
            ManualText.Text = "GDD-MANUAL.md not found.\n\nExpected at:\n" + manualPath;
    }

    private void OnClose(object sender, RoutedEventArgs e) => Close();
}
