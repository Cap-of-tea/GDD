using System.Collections.Generic;
using Avalonia.Controls;
using Avalonia.Input;
using Avalonia.Interactivity;
using GDD.Desktop.ViewModels;
using GDD.Models;

namespace GDD.Desktop.Views;

public partial class MainWindow : Window
{
    public MainWindow()
    {
        InitializeComponent();
    }

    private void OnOpenAddMenu(object? sender, RoutedEventArgs e)
    {
        if (sender is not Control target || DataContext is not MainViewModel vm)
            return;

        var flyout = new MenuFlyout();

        // --- Multi-device test presets ---
        flyout.Items.Add(new MenuItem { Header = "Presets", IsEnabled = false });
        foreach (var preset in DeviceTestPreset.All)
        {
            flyout.Items.Add(new MenuItem
            {
                Header = $"{preset.Name} ({preset.Devices.Length})",
                Command = vm.AddPresetCommand,
                CommandParameter = preset
            });
        }

        flyout.Items.Add(new Separator());

        // --- Single device, grouped by category (includes every desktop screen) ---
        AddDeviceSubmenu(flyout, vm, "Phone", DevicePresets.Phones);
        AddDeviceSubmenu(flyout, vm, "Tablet", DevicePresets.Tablets);
        AddDeviceSubmenu(flyout, vm, "Desktop", DevicePresets.Desktops);

        flyout.ShowAt(target);
    }

    private static void AddDeviceSubmenu(
        MenuFlyout flyout, MainViewModel vm, string title, IReadOnlyList<DevicePreset> devices)
    {
        var parent = new MenuItem { Header = title };
        foreach (var d in devices)
        {
            parent.Items.Add(new MenuItem
            {
                Header = $"{d.Name}  ({d.Width}×{d.Height})",
                Command = vm.AddDeviceCommand,
                CommandParameter = d
            });
        }
        flyout.Items.Add(parent);
    }

    private async void OnMascotClicked(object? sender, PointerPressedEventArgs e)
    {
        // Peek out, then fade back (Opacity transition animates both changes).
        Mascot.Opacity = 0.85;
        await Task.Delay(2500);
        Mascot.Opacity = 0.08;
    }
}
