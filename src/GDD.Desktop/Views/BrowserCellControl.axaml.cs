using Avalonia.Controls;
using Avalonia.Input;
using Avalonia.VisualTree;
using GDD.Desktop.Platform;
using GDD.Desktop.ViewModels;

namespace GDD.Desktop.Views;

public partial class BrowserCellControl : UserControl
{
    public BrowserCellControl()
    {
        InitializeComponent();
    }

    private void OnCellPressed(object? sender, PointerPressedEventArgs e)
    {
        if (!e.GetCurrentPoint(this).Properties.IsLeftButtonPressed) return;
        if (DataContext is not DesktopPlayerContext player) return;

        if ((this.GetVisualRoot() as Window)?.DataContext is MainViewModel vm
            && vm.BringToFrontCommand.CanExecute(player))
        {
            vm.BringToFrontCommand.Execute(player);
        }
    }
}
