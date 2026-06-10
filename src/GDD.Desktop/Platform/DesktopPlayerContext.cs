using Avalonia.Media.Imaging;
using CommunityToolkit.Mvvm.ComponentModel;
using GDD.Abstractions;
using GDD.Models;

namespace GDD.Desktop.Platform;

/// <summary>
/// Observable player context for GDD.Desktop. Same data as HeadlessPlayerContext
/// but as ObservableObject for data binding, plus a live Thumbnail bitmap.
/// </summary>
public sealed partial class DesktopPlayerContext : ObservableObject, IPlayerContext
{
    private readonly TaskCompletionSource _engineTcs = new(TaskCreationOptions.RunContinuationsAsynchronously);

    public int PlayerId { get; }
    public string PlayerName { get; }

    [ObservableProperty] private string _currentUrl;
    [ObservableProperty] private string _statusText = "Initializing...";
    [ObservableProperty] private string _networkStatus = "Online";
    [ObservableProperty] private int _notificationCount;
    [ObservableProperty] private DevicePreset _selectedDevice = DevicePresets.Default;
    [ObservableProperty] private LocationPreset? _selectedLocation;
    [ObservableProperty] private NetworkPreset _selectedNetwork = NetworkPresets.Online;
    [ObservableProperty] private int _consoleErrorCount;
    [ObservableProperty] private int _networkErrorCount;
    [ObservableProperty] private string _lastError = "";
    [ObservableProperty] private string _language = "";

    [ObservableProperty] private Bitmap? _thumbnail;

    /// <summary>Previous frame's bitmap, disposed two frames later to avoid a render race.</summary>
    internal Bitmap? PendingDispose { get; set; }

    public bool IsOverlayOpen => false;
    public string? OwnerSessionId { get; set; }
    public IBrowserEngine? Engine { get; set; }
    public Task EngineReady => _engineTcs.Task;

    public void SetEngineReady() => _engineTcs.TrySetResult();
    public void SetEngineFailed() => _engineTcs.TrySetResult();

    // Telegram emulation state (set via settings dialog).
    public bool TelegramEnabled { get; set; }
    public long TelegramUserId { get; set; }
    public string TelegramUsername { get; set; } = "";
    public string TelegramFirstName { get; set; } = "";
    public string TelegramLanguageCode { get; set; } = "en";

    public DesktopPlayerContext(int playerId, string defaultUrl)
    {
        PlayerId = playerId;
        PlayerName = $"Player {playerId}";
        _currentUrl = defaultUrl;
    }
}
