using System.IO;
using System.Windows;
using System.Windows.Media;
using CommunityToolkit.Mvvm.ComponentModel;
using CommunityToolkit.Mvvm.Input;
using Microsoft.Web.WebView2.Wpf;
using GDD.Abstractions;
using GDD.Engines;
using GDD.Models;

namespace GDD.ViewModels;

public partial class BrowserCellViewModel : ObservableObject, IPlayerContext, IDisposable
{
    private readonly AppConfig _config;

    public int PlayerId { get; }

    [ObservableProperty]
    private string _playerName;

    [ObservableProperty]
    private string _currentUrl;

    [ObservableProperty]
    private bool _isOverlayOpen;

    [ObservableProperty]
    private bool _isLoading;

    [ObservableProperty]
    private string _statusText = "Initializing...";

    [ObservableProperty]
    private int _notificationCount;

    [ObservableProperty]
    private string _networkStatus = "Online";

    [ObservableProperty]
    private Brush _networkIndicatorBrush = Brushes.LimeGreen;

    [ObservableProperty]
    private DevicePreset _selectedDevice = DevicePresets.Default;

    [ObservableProperty]
    private LocationPreset? _selectedLocation;

    [ObservableProperty]
    private NetworkPreset _selectedNetwork = NetworkPresets.Online;

    [ObservableProperty]
    private bool _telegramEnabled;

    [ObservableProperty]
    private long _telegramUserId;

    [ObservableProperty]
    private string _telegramUsername = "";

    [ObservableProperty]
    private string _telegramFirstName = "";

    [ObservableProperty]
    private string _telegramLanguageCode = "en";

    [ObservableProperty]
    private int _consoleErrorCount;

    [ObservableProperty]
    private int _networkErrorCount;

    [ObservableProperty]
    private string _lastError = "";

    [ObservableProperty]
    private string _language = "";

    public string? OwnerSessionId { get; set; }

    public bool HasNotifications => NotificationCount > 0;

    [ObservableProperty]
    private double _dwmThumbnailHeight = 350;

    public WebView2? WebView { get; set; }
    public Window? OverlayWindow { get; set; }

    public IBrowserEngine? Engine { get; set; }

    public Action<BrowserCellViewModel>? OnSettingsRequested { get; set; }
    public Action<BrowserCellViewModel>? OnOverlayRequested { get; set; }
    public Action<BrowserCellViewModel>? OnWebViewReady { get; set; }
    public Action<BrowserCellViewModel, DevicePreset>? OnDeviceChanged { get; set; }

    public BrowserCellViewModel(int playerId, AppConfig config, string defaultUrl)
    {
        PlayerId = playerId;
        _config = config;
        _playerName = $"Player {playerId}";
        _currentUrl = defaultUrl;
    }

    public string UserDataFolder =>
        Path.Combine(_config.GetDataFolderRoot(), $"Player_{PlayerId}");

    partial void OnNotificationCountChanged(int value)
    {
        OnPropertyChanged(nameof(HasNotifications));
    }

    partial void OnSelectedDeviceChanged(DevicePreset value)
    {
        OnDeviceChanged?.Invoke(this, value);
    }

    public void UpdateNetworkIndicator(NetworkPreset preset)
    {
        NetworkStatus = preset.Name;
        NetworkIndicatorBrush = preset.Name switch
        {
            "Offline" => Brushes.Red,
            "Slow 3G" => Brushes.Orange,
            "Fast 3G" => Brushes.Yellow,
            _ => Brushes.LimeGreen
        };
    }

    [RelayCommand]
    private void OpenSettings()
    {
        OnSettingsRequested?.Invoke(this);
    }

    [RelayCommand]
    private void OpenOverlay()
    {
        OnOverlayRequested?.Invoke(this);
    }

    public void NotifyWebViewReady()
    {
        OnWebViewReady?.Invoke(this);
    }

    public void Dispose()
    {
        WebView?.Dispose();
        WebView = null;
        GC.SuppressFinalize(this);
    }
}
