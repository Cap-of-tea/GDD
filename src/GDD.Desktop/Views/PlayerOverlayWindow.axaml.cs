using Avalonia;
using Avalonia.Controls;
using Avalonia.Input;
using Avalonia.Interactivity;
using GDD.Desktop.Engines;
using GDD.Desktop.Platform;
using GDD.Desktop.Services;

namespace GDD.Desktop.Views;

public partial class PlayerOverlayWindow : Window
{
    private readonly DesktopPlayerContext _player = null!;
    private readonly IThumbnailService _thumbnails = null!;
    private readonly PlaywrightHeadedEngine? _engine;

    public PlayerOverlayWindow()
    {
        InitializeComponent();
    }

    public PlayerOverlayWindow(DesktopPlayerContext player, IThumbnailService thumbnails) : this()
    {
        _player = player;
        _thumbnails = thumbnails;
        _engine = player.Engine as PlaywrightHeadedEngine;
        DataContext = player;

        var d = player.SelectedDevice;
        Title = $"{player.PlayerName} — {d.Name}";

        // Fit window to device aspect, capped to a sensible height.
        var maxH = 980.0;
        var scale = d.Height > maxH ? maxH / d.Height : 1.0;
        Width = d.Width * scale;
        Height = d.Height * scale;

        Opened += (_, _) => _thumbnails.SetFocused(player.PlayerId, true);
        Closed += (_, _) => _thumbnails.SetFocused(player.PlayerId, false);

        LiveImage.PointerPressed += OnPointerPressed;
        LiveImage.PointerReleased += OnPointerReleased;
        LiveImage.PointerMoved += OnPointerMoved;
        LiveImage.PointerWheelChanged += OnWheel;
        AddHandler(KeyDownEvent, OnKeyDown, RoutingStrategies.Tunnel);
        AddHandler(TextInputEvent, OnTextInput, RoutingStrategies.Tunnel);
    }

    private (double x, double y) ToCss(PointerEventArgs e)
    {
        var pos = e.GetPosition(LiveImage);
        var w = LiveImage.Bounds.Width;
        var h = LiveImage.Bounds.Height;
        var d = _player.SelectedDevice;
        var x = w > 0 ? Math.Clamp(pos.X / w * d.Width, 0, d.Width) : 0;
        var y = h > 0 ? Math.Clamp(pos.Y / h * d.Height, 0, d.Height) : 0;
        return (x, y);
    }

    private static string ButtonName(PointerUpdateKind k) => k switch
    {
        PointerUpdateKind.LeftButtonPressed or PointerUpdateKind.LeftButtonReleased => "left",
        PointerUpdateKind.RightButtonPressed or PointerUpdateKind.RightButtonReleased => "right",
        PointerUpdateKind.MiddleButtonPressed or PointerUpdateKind.MiddleButtonReleased => "middle",
        _ => "left"
    };

    private void OnPointerPressed(object? sender, PointerPressedEventArgs e)
    {
        if (_engine is null) return;
        var (x, y) = ToCss(e);
        var btn = ButtonName(e.GetCurrentPoint(LiveImage).Properties.PointerUpdateKind);
        LiveImage.Focus();
        _ = _engine.DispatchMouseAsync("mousePressed", x, y, btn, e.ClickCount);
    }

    private void OnPointerReleased(object? sender, PointerReleasedEventArgs e)
    {
        if (_engine is null) return;
        var (x, y) = ToCss(e);
        var btn = ButtonName(e.GetCurrentPoint(LiveImage).Properties.PointerUpdateKind);
        _ = _engine.DispatchMouseAsync("mouseReleased", x, y, btn, 1);
    }

    private void OnPointerMoved(object? sender, PointerEventArgs e)
    {
        if (_engine is null) return;
        var (x, y) = ToCss(e);
        _ = _engine.DispatchMouseAsync("mouseMoved", x, y);
    }

    private void OnWheel(object? sender, PointerWheelEventArgs e)
    {
        if (_engine is null) return;
        var (x, y) = ToCss(e);
        _ = _engine.DispatchWheelAsync(x, y, -e.Delta.X * 100, -e.Delta.Y * 100);
    }

    private void OnTextInput(object? sender, TextInputEventArgs e)
    {
        if (_engine is null || string.IsNullOrEmpty(e.Text)) return;
        _ = _engine.InsertTextAsync(e.Text);
    }

    private static (string key, int vk)? SpecialKey(Key k) => k switch
    {
        Key.Enter => ("Enter", 13),
        Key.Back => ("Backspace", 8),
        Key.Tab => ("Tab", 9),
        Key.Delete => ("Delete", 46),
        Key.Escape => ("Escape", 27),
        Key.Left => ("ArrowLeft", 37),
        Key.Up => ("ArrowUp", 38),
        Key.Right => ("ArrowRight", 39),
        Key.Down => ("ArrowDown", 40),
        Key.Home => ("Home", 36),
        Key.End => ("End", 35),
        Key.PageUp => ("PageUp", 33),
        Key.PageDown => ("PageDown", 34),
        _ => null
    };

    private async void OnKeyDown(object? sender, KeyEventArgs e)
    {
        if (_engine is null) return;
        if (SpecialKey(e.Key) is not { } sk) return; // printable chars handled by TextInput
        e.Handled = true;
        await _engine.DispatchKeyAsync("keyDown", sk.key, sk.vk);
        await _engine.DispatchKeyAsync("keyUp", sk.key, sk.vk);
    }
}
