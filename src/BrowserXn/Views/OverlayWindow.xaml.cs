using System.Runtime.InteropServices;
using System.Text.Json;
using System.Windows;
using System.Windows.Controls;
using System.Windows.Input;
using System.Windows.Interop;
using Microsoft.Web.WebView2.Core;
using Microsoft.Web.WebView2.Wpf;
using GDD.Interop;
using GDD.Models;
using GDD.ViewModels;
using Serilog;

namespace GDD.Views;

public partial class OverlayWindow : Window
{
    private static readonly ILogger Logger = Log.ForContext<OverlayWindow>();

    private WebView2? _webView;
    private bool _initialized;
    private bool _forceClosing;
    private double _aspectRatio;
    private double _chromeWPx;
    private double _chromeHPx;

    public OverlayWindow()
    {
        InitializeComponent();
        Loaded += OnLoaded;
        Closing += OnClosing;
    }

    private void MoveOffScreen()
    {
        Left = -10000;
        Top = -10000;
    }

    private async void OnLoaded(object sender, RoutedEventArgs e)
    {
        if (_initialized || DataContext is not OverlayViewModel vm)
            return;

        _initialized = true;
        SetupProportionalResize();

        vm.CloseRequested += (_, _) =>
        {
            MoveOffScreen();
            vm.Cell.IsOverlayOpen = false;
        };

        try
        {
            _webView = new WebView2
            {
                DefaultBackgroundColor = System.Drawing.Color.FromArgb(26, 26, 37)
            };

            if (WebViewContainer is Border border)
                border.Child = _webView;

            var env = await CoreWebView2Environment.CreateAsync(
                browserExecutableFolder: null,
                userDataFolder: vm.Cell.UserDataFolder);
            await _webView.EnsureCoreWebView2Async(env);

            _webView.CoreWebView2.Settings.IsStatusBarEnabled = false;
            _webView.CoreWebView2.Settings.AreDefaultContextMenusEnabled = true;
            _webView.CoreWebView2.Settings.IsZoomControlEnabled = false;

            var device = vm.Cell.SelectedDevice;
            _aspectRatio = (double)device.Width / device.Height;

            await _webView.CoreWebView2.CallDevToolsProtocolMethodAsync(
                "Emulation.setDeviceMetricsOverride",
                JsonSerializer.Serialize(new
                {
                    width = device.Width,
                    height = device.Height,
                    deviceScaleFactor = device.DeviceScaleFactor,
                    mobile = device.IsMobile
                }));
            await _webView.CoreWebView2.CallDevToolsProtocolMethodAsync(
                "Emulation.setUserAgentOverride",
                JsonSerializer.Serialize(new { userAgent = device.UserAgent }));
            await _webView.CoreWebView2.CallDevToolsProtocolMethodAsync(
                "Emulation.setTouchEmulationEnabled",
                JsonSerializer.Serialize(new { enabled = device.HasTouch, maxTouchPoints = 5 }));

            _webView.CoreWebView2.PermissionRequested += (_, args) =>
            {
                if (args.PermissionKind is CoreWebView2PermissionKind.Notifications
                    or CoreWebView2PermissionKind.Geolocation)
                {
                    args.State = CoreWebView2PermissionState.Allow;
                }
            };

            _webView.CoreWebView2.NavigationCompleted += (_, _) =>
            {
                Dispatcher.InvokeAsync(() =>
                {
                    if (_webView?.CoreWebView2 is null) return;
                    vm.Cell.CurrentUrl = _webView.CoreWebView2.Source;
                    vm.Cell.StatusText = _webView.CoreWebView2.DocumentTitle;
                });
            };

            vm.Cell.WebView = _webView;
            vm.Cell.OnDeviceChanged = OnDeviceChanged;
            vm.Cell.NotifyWebViewReady();
            _webView.CoreWebView2.Navigate(vm.Cell.CurrentUrl);
            vm.Cell.StatusText = "Loading...";

            Logger.Information("WebView2 initialized for {Player}", vm.Cell.PlayerName);
        }
        catch (Exception ex)
        {
            Logger.Error(ex, "Failed to initialize WebView2 for {Player}", vm.Cell.PlayerName);
            vm.Cell.StatusText = $"Error: {ex.Message}";
        }
    }

    private void OnClosing(object? sender, System.ComponentModel.CancelEventArgs e)
    {
        if (!_forceClosing)
        {
            e.Cancel = true;
            MoveOffScreen();
            if (DataContext is OverlayViewModel vm)
                vm.Cell.IsOverlayOpen = false;
            return;
        }

        if (_webView is not null)
        {
            if (WebViewContainer is Border border)
                border.Child = null;
            _webView.Dispose();
            _webView = null;
        }

        if (DataContext is OverlayViewModel vm2)
        {
            vm2.Cell.WebView = null;
            vm2.Cell.IsOverlayOpen = false;
        }

        Logger.Information("Overlay destroyed");
    }

    private void OnDeviceChanged(BrowserCellViewModel cell, DevicePreset device)
    {
        Dispatcher.Invoke(() =>
        {
            _aspectRatio = (double)device.Width / device.Height;
            Width = device.Width + 4;
            Height = device.Height + 40;

            if (cell.IsOverlayOpen)
            {
                Left = (SystemParameters.WorkArea.Width - Width) / 2;
                Top = (SystemParameters.WorkArea.Height - Height) / 2;
            }
            else
            {
                MoveOffScreen();
            }

            if (DataContext is OverlayViewModel vm)
                vm.Title = $"{cell.PlayerName} — {device.Name}";
        });
    }

    public void ForceClose()
    {
        _forceClosing = true;
        Close();
    }

    private void OnOpenDevTools(object sender, RoutedEventArgs e)
    {
        _webView?.CoreWebView2?.OpenDevToolsWindow();
    }

    private void OnKeyDown(object sender, KeyEventArgs e)
    {
        if (e.Key == Key.Escape)
        {
            MoveOffScreen();
            if (DataContext is OverlayViewModel vm)
                vm.Cell.IsOverlayOpen = false;
        }
    }

    private void OnTitleBarMouseDown(object sender, MouseButtonEventArgs e)
    {
        if (e.ChangedButton == MouseButton.Left)
            DragMove();
    }

    private void SetupProportionalResize()
    {
        var hwnd = new WindowInteropHelper(this).Handle;
        var source = HwndSource.FromHwnd(hwnd);
        source?.AddHook(WndProc);

        var ps = PresentationSource.FromVisual(this);
        if (ps?.CompositionTarget is not null)
        {
            var toDevice = ps.CompositionTarget.TransformToDevice;
            _chromeWPx = 4 * toDevice.M11;
            _chromeHPx = 40 * toDevice.M22;
        }
        else
        {
            _chromeWPx = 4;
            _chromeHPx = 40;
        }
    }

    private IntPtr WndProc(IntPtr hwnd, int msg, IntPtr wParam, IntPtr lParam, ref bool handled)
    {
        const int WM_SIZING = 0x0214;

        if (msg == WM_SIZING && _aspectRatio > 0)
        {
            var rect = Marshal.PtrToStructure<DwmApi.RECT>(lParam);
            int edge = wParam.ToInt32();

            double w = rect.Right - rect.Left;
            double h = rect.Bottom - rect.Top;

            if (edge is 3 or 6)
            {
                double contentH = h - _chromeHPx;
                double newW = contentH * _aspectRatio + _chromeWPx;
                rect.Right = rect.Left + (int)Math.Round(newW);
            }
            else
            {
                double contentW = w - _chromeWPx;
                double newH = contentW / _aspectRatio + _chromeHPx;
                if (edge is 3 or 4 or 5)
                    rect.Top = rect.Bottom - (int)Math.Round(newH);
                else
                    rect.Bottom = rect.Top + (int)Math.Round(newH);
            }

            Marshal.StructureToPtr(rect, lParam, false);
            handled = true;
        }

        return IntPtr.Zero;
    }
}
