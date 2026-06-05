using Avalonia.Controls;
using Avalonia.Input;
using Avalonia.Interactivity;
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

    private (MainViewModel vm, DesktopPlayerContext player)? Resolve()
    {
        if (DataContext is not DesktopPlayerContext player) return null;
        if ((this.GetVisualRoot() as Window)?.DataContext is not MainViewModel vm) return null;
        return (vm, player);
    }

    private void OnCellPressed(object? sender, PointerPressedEventArgs e)
    {
        if (!e.GetCurrentPoint(this).Properties.IsLeftButtonPressed) return;
        if (Resolve() is not { } r) return;
        if (r.vm.BringToFrontCommand.CanExecute(r.player))
            r.vm.BringToFrontCommand.Execute(r.player);
    }

    private void OnSettingsClicked(object? sender, RoutedEventArgs e)
    {
        if (Resolve() is not { } r) return;
        if (r.vm.OpenSettingsCommand.CanExecute(r.player))
            r.vm.OpenSettingsCommand.Execute(r.player);
    }

    private void OnRemoveClicked(object? sender, RoutedEventArgs e)
    {
        if (Resolve() is not { } r) return;
        if (r.vm.RemovePlayerCommand.CanExecute(r.player))
            r.vm.RemovePlayerCommand.Execute(r.player);
    }
}
