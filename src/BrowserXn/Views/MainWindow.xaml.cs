using System.Windows;
using System.Windows.Controls;
using System.Windows.Input;
using System.Windows.Interop;
using System.Windows.Threading;
using GDD.Interop;
using GDD.Models;
using GDD.ViewModels;

namespace GDD.Views;

public partial class MainWindow : Window
{
    private readonly DispatcherTimer _mascotTimer;

    public MainWindow()
    {
        InitializeComponent();
        SourceInitialized += (_, _) => DarkTitleBar.Apply(this);
        Closing += OnWindowClosing;
        _mascotTimer = new DispatcherTimer { Interval = TimeSpan.FromSeconds(5) };
        _mascotTimer.Tick += (_, _) =>
        {
            _mascotTimer.Stop();
            MascotImage.Opacity = 1.0;
        };
    }

    private void OnWindowClosing(object? sender, System.ComponentModel.CancelEventArgs e)
    {
        if (DataContext is MainViewModel vm)
            vm.CloseAllCommand.Execute(null);
    }

    private void OnWindowLoaded(object sender, RoutedEventArgs e)
    {
        var screenW = SystemParameters.WorkArea.Width;
        var screenH = SystemParameters.WorkArea.Height;
        Width = screenW * 0.3;
        Height = screenH * 0.3;
        Left = (screenW - Width) / 2;
        Top = (screenH - Height) / 2;
    }

    private void OnMascotMouseDown(object sender, MouseButtonEventArgs e)
    {
        MascotImage.CaptureMouse();
        _mascotTimer.Start();
    }

    private void OnMascotMouseUp(object sender, MouseButtonEventArgs e)
    {
        _mascotTimer.Stop();
        MascotImage.ReleaseMouseCapture();
        MascotImage.Opacity = 0.1;
    }

    private void OnOpenHelp(object sender, RoutedEventArgs e)
    {
        var help = new HelpWindow { Owner = this };
        help.Show();
    }

    private void OnOpenPresetMenu(object sender, RoutedEventArgs e)
    {
        if (DataContext is not MainViewModel vm) return;

        var menu = new ContextMenu();
        foreach (var preset in DeviceTestPreset.All)
        {
            var item = new MenuItem
            {
                Header = $"{preset.Name} ({preset.Devices.Length} devices)",
                Command = vm.AddPresetCommand,
                CommandParameter = preset
            };
            menu.Items.Add(item);
        }

        menu.PlacementTarget = (Button)sender;
        menu.Placement = System.Windows.Controls.Primitives.PlacementMode.Bottom;
        menu.IsOpen = true;
    }
}
