using Avalonia.Controls;
using Avalonia.Input;

namespace GDD.Desktop.Views;

public partial class MainWindow : Window
{
    public MainWindow()
    {
        InitializeComponent();
    }

    private async void OnMascotClicked(object? sender, PointerPressedEventArgs e)
    {
        // Peek out, then fade back (Opacity transition animates both changes).
        Mascot.Opacity = 0.85;
        await Task.Delay(2500);
        Mascot.Opacity = 0.08;
    }
}
