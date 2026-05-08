using System.Windows;
using System.Windows.Controls;
using System.Windows.Input;
using System.Windows.Interop;
using System.Windows.Media;
using GDD.Interop;
using GDD.ViewModels;

namespace GDD.Views;

public partial class BrowserCellControl : UserControl
{
    private Window? _thumbWindow;
    private IntPtr _thumbId;
    private bool _thumbRegistered;

    public BrowserCellControl()
    {
        InitializeComponent();
        Loaded += OnLoaded;
        Unloaded += OnUnloaded;
    }

    private void OnLoaded(object sender, RoutedEventArgs e)
    {
        if (DataContext is not BrowserCellViewModel vm || vm.OverlayWindow is null)
            return;

        SetupThumbnail(vm);
        CompositionTarget.Rendering += OnRendering;
    }

    private void OnUnloaded(object sender, RoutedEventArgs e)
    {
        CompositionTarget.Rendering -= OnRendering;
        DestroyThumbnail();
    }

    private void SetupThumbnail(BrowserCellViewModel vm)
    {
        var mainWindow = Window.GetWindow(this);
        if (mainWindow is null || vm.OverlayWindow is null) return;

        _thumbWindow = new Window
        {
            WindowStyle = WindowStyle.None,
            AllowsTransparency = false,
            Background = new SolidColorBrush(Color.FromRgb(26, 26, 37)),
            ShowInTaskbar = false,
            ResizeMode = ResizeMode.NoResize,
            Owner = mainWindow,
            Left = -10000,
            Top = -10000,
            Width = 1,
            Height = 1
        };
        _thumbWindow.Cursor = Cursors.Hand;
        _thumbWindow.MouseLeftButtonDown += (_, _) =>
        {
            if (DataContext is BrowserCellViewModel cellVm)
                cellVm.OpenOverlayCommand.Execute(null);
        };
        _thumbWindow.MouseRightButtonDown += (_, args) =>
        {
            if (DataContext is BrowserCellViewModel cellVm)
                ShowContextMenu(cellVm);
            args.Handled = true;
        };
        _thumbWindow.Show();

        var thumbHwnd = new WindowInteropHelper(_thumbWindow).Handle;
        var exStyle = DwmApi.GetWindowLong(thumbHwnd, DwmApi.GWL_EXSTYLE);
        DwmApi.SetWindowLong(thumbHwnd, DwmApi.GWL_EXSTYLE,
            exStyle | DwmApi.WS_EX_TOOLWINDOW);

        var overlayHwnd = new WindowInteropHelper(vm.OverlayWindow).Handle;
        if (overlayHwnd != IntPtr.Zero && thumbHwnd != IntPtr.Zero)
        {
            var hr = DwmApi.DwmRegisterThumbnail(thumbHwnd, overlayHwnd, out _thumbId);
            _thumbRegistered = hr == 0;
        }
    }

    private void OnRendering(object? sender, EventArgs e)
    {
        if (_thumbWindow is null || !_thumbRegistered)
            return;

        var source = PresentationSource.FromVisual(this);
        if (source?.CompositionTarget is null) return;

        if (ThumbnailArea.ActualWidth < 1 || ThumbnailArea.ActualHeight < 1)
        {
            if (_thumbWindow.Left != -10000)
            {
                _thumbWindow.Left = -10000;
                _thumbWindow.Top = -10000;
            }
            return;
        }

        var screenPoint = ThumbnailArea.PointToScreen(new Point(0, 0));
        var fromDevice = source.CompositionTarget.TransformFromDevice;
        var toDevice = source.CompositionTarget.TransformToDevice;
        var dipPoint = fromDevice.Transform(screenPoint);

        _thumbWindow.Left = dipPoint.X;
        _thumbWindow.Top = dipPoint.Y;
        _thumbWindow.Width = ThumbnailArea.ActualWidth;
        _thumbWindow.Height = ThumbnailArea.ActualHeight;

        var pixelWidth = (int)(ThumbnailArea.ActualWidth * toDevice.M11);
        var pixelHeight = (int)(ThumbnailArea.ActualHeight * toDevice.M22);

        var props = new DwmApi.DwmThumbnailProperties
        {
            dwFlags = DwmApi.DWM_TNP_RECTDESTINATION | DwmApi.DWM_TNP_VISIBLE
                    | DwmApi.DWM_TNP_SOURCECLIENTAREAONLY | DwmApi.DWM_TNP_OPACITY,
            rcDestination = new DwmApi.RECT(0, 0, pixelWidth, pixelHeight),
            fVisible = true,
            fSourceClientAreaOnly = true,
            opacity = 255
        };
        DwmApi.DwmUpdateThumbnailProperties(_thumbId, ref props);

        PlaceholderText.Visibility = Visibility.Collapsed;
    }

    private void DestroyThumbnail()
    {
        if (_thumbRegistered && _thumbId != IntPtr.Zero)
        {
            DwmApi.DwmUnregisterThumbnail(_thumbId);
            _thumbId = IntPtr.Zero;
            _thumbRegistered = false;
        }

        if (_thumbWindow is not null)
        {
            _thumbWindow.Close();
            _thumbWindow = null;
        }
    }

    private void ShowContextMenu(BrowserCellViewModel vm)
    {
        var menu = new ContextMenu();

        var expand = new MenuItem { Header = "Expand" };
        expand.Click += (_, _) => vm.OpenOverlayCommand.Execute(null);
        menu.Items.Add(expand);

        var settings = new MenuItem { Header = "Settings" };
        settings.Click += (_, _) => vm.OpenSettingsCommand.Execute(null);
        menu.Items.Add(settings);

        menu.Items.Add(new Separator());

        var remove = new MenuItem { Header = "Remove" };
        remove.Click += (_, _) =>
        {
            if (Window.GetWindow(this)?.DataContext is MainViewModel mainVm)
                mainVm.RemovePlayerCommand.Execute(vm);
        };
        menu.Items.Add(remove);

        menu.PlacementTarget = _thumbWindow;
        menu.IsOpen = true;
    }

    private void OnThumbnailClicked(object sender, MouseButtonEventArgs e)
    {
        if (DataContext is BrowserCellViewModel vm)
            vm.OpenOverlayCommand.Execute(null);
    }
}
