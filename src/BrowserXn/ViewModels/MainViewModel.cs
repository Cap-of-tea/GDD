using System.Collections.ObjectModel;
using System.Threading;
using System.Windows;
using CommunityToolkit.Mvvm.ComponentModel;
using CommunityToolkit.Mvvm.Input;
using GDD.Models;
using GDD.Services;
using GDD.Views;
using Serilog;

namespace GDD.ViewModels;

public partial class MainViewModel : ObservableObject
{
    private static readonly ILogger Logger = Log.ForContext<MainViewModel>();
    private readonly AppConfig _config;
    private readonly DeviceEmulationService _deviceService;
    private readonly LocationEmulationService _locationService;
    private readonly NetworkEmulationService _networkService;
    private readonly TelegramInjectionService _telegramService;
    private readonly QuickAuthService _authService;
    private readonly TokenInjectionService _tokenService;
    private readonly NotificationInterceptionService _notificationService;
    private readonly ConsoleInterceptionService _consoleService;
    private readonly NetworkMonitoringService _networkMonitorService;
    private int _nextPlayerId = 1;

    [ObservableProperty]
    private int _playerCount;

    [ObservableProperty]
    private string _statusText = "Ready";

    [ObservableProperty]
    private string _defaultUrl;

    public ObservableCollection<BrowserCellViewModel> Players { get; } = new();

    public MainViewModel(
        AppConfig config,
        DeviceEmulationService deviceService,
        LocationEmulationService locationService,
        NetworkEmulationService networkService,
        TelegramInjectionService telegramService,
        QuickAuthService authService,
        TokenInjectionService tokenService,
        NotificationInterceptionService notificationService,
        ConsoleInterceptionService consoleService,
        NetworkMonitoringService networkMonitorService)
    {
        _config = config;
        _deviceService = deviceService;
        _locationService = locationService;
        _networkService = networkService;
        _telegramService = telegramService;
        _authService = authService;
        _tokenService = tokenService;
        _notificationService = notificationService;
        _consoleService = consoleService;
        _networkMonitorService = networkMonitorService;
        _defaultUrl = config.FrontendUrl;
    }

    [RelayCommand]
    private void AddPlayer()
    {
        AddPlayerWithDevice(DevicePresets.Default);
    }

    [RelayCommand]
    private void AddPreset(DeviceTestPreset? preset)
    {
        if (preset is null) return;

        foreach (var device in preset.Devices)
            AddPlayerWithDevice(device);

        StatusText = $"Added {preset.Devices.Length} players ({preset.Name})";
    }

    private void AddPlayerWithDevice(DevicePreset device)
    {
        var playerId = _nextPlayerId++;
        var vm = new BrowserCellViewModel(playerId, _config, DefaultUrl)
        {
            SelectedDevice = device
        };
        vm.OnSettingsRequested = OnPlayerSettingsRequested;
        vm.OnOverlayRequested = OnPlayerOverlayRequested;
        vm.OnWebViewReady = OnPlayerWebViewReady;

        var overlayVm = new OverlayViewModel(vm, device.Name, device.Width, device.Height);
        var overlay = new OverlayWindow { DataContext = overlayVm };
        vm.OverlayWindow = overlay;

        overlay.Width = device.Width + 4;
        overlay.Height = device.Height + 40;
        overlay.Left = -10000;
        overlay.Top = -10000;
        overlay.Show();

        Players.Add(vm);
        PlayerCount = Players.Count;
    }

    [RelayCommand]
    private void RemovePlayer(BrowserCellViewModel? player)
    {
        if (player is null) return;
        Players.Remove(player);

        _consoleService.Remove(player.PlayerId);
        _networkMonitorService.Remove(player.PlayerId);

        if (player.OverlayWindow is OverlayWindow overlay)
            overlay.ForceClose();

        player.Dispose();
        PlayerCount = Players.Count;
        StatusText = $"Players: {PlayerCount}";
    }

    [RelayCommand]
    private async Task QuickAuthAllAsync()
    {
        if (Players.Count == 0) return;

        StatusText = "Quick Auth: authenticating all players...";
        var targets = Players.ToList();
        var authenticated = 0;

        foreach (var batch in targets.Chunk(8))
        {
            var tasks = batch.Select(async player =>
            {
                try
                {
                    if (player.WebView?.CoreWebView2 is null) return;

                    var authResult = await _authService.RegisterAndLoginAsync(player.PlayerId);
                    if (authResult is null) return;

                    await Application.Current.Dispatcher.InvokeAsync(
                        () => _tokenService.InjectAsync(
                            player.WebView.CoreWebView2, authResult, _config.FrontendUrl));

                    Interlocked.Increment(ref authenticated);
                    player.StatusText = $"Authenticated as {authResult.User?.Username}";
                }
                catch (Exception ex)
                {
                    Logger.Error(ex, "Quick Auth failed for Player {Id}", player.PlayerId);
                    player.StatusText = $"Auth error: {ex.Message}";
                }
            });

            await Task.WhenAll(tasks);
            if (batch.Length == 8)
                await Task.Delay(500);
        }

        StatusText = $"Quick Auth: {authenticated}/{targets.Count} authenticated";
    }

    [RelayCommand]
    private void CloseAll()
    {
        var count = Players.Count;
        if (count == 0) return;

        foreach (var player in Players.ToList())
        {
            _consoleService.Remove(player.PlayerId);
            _networkMonitorService.Remove(player.PlayerId);

            if (player.OverlayWindow is OverlayWindow overlay)
                overlay.ForceClose();
            player.Dispose();
        }

        Players.Clear();
        PlayerCount = 0;
        _nextPlayerId = 1;
        StatusText = $"Closed {count} players";
    }

    [RelayCommand]
    private void NavigateAll()
    {
        var url = DefaultUrl?.Trim();
        if (string.IsNullOrEmpty(url)) return;
        if (!url.Contains("://"))
            url = "https://" + url;

        foreach (var player in Players)
        {
            player.WebView?.CoreWebView2?.Navigate(url);
            player.CurrentUrl = url;
        }

        StatusText = $"Navigating all to {url}";
    }

    private void OnPlayerSettingsRequested(BrowserCellViewModel vm)
    {
        var window = new CellSettingsWindow(vm, DefaultUrl)
        {
            Owner = Application.Current.MainWindow
        };

        if (window.ShowDialog() != true)
            return;

        _ = ApplySettingsAsync(vm, window);
    }

    private async Task ApplySettingsAsync(BrowserCellViewModel vm, CellSettingsWindow settings)
    {
        var webView = vm.WebView?.CoreWebView2;
        if (webView is null)
        {
            Logger.Warning("Cannot apply settings — WebView2 not initialized for {Player}", vm.PlayerName);
            return;
        }

        try
        {
            vm.SelectedDevice = settings.SelectedDevice;
            await _deviceService.ApplyAsync(webView, settings.SelectedDevice);

            vm.SelectedLocation = settings.SelectedLocation;
            if (settings.SelectedLocation is not null)
                await _locationService.ApplyAsync(webView, settings.SelectedLocation);
            else
                await _locationService.ClearAsync(webView);

            vm.SelectedNetwork = settings.SelectedNetwork;
            await _networkService.ApplyAsync(webView, settings.SelectedNetwork);
            vm.UpdateNetworkIndicator(settings.SelectedNetwork);

            if (!string.IsNullOrEmpty(settings.NavigateUrl))
            {
                var url = settings.NavigateUrl;
                if (!url.Contains("://"))
                    url = "https://" + url;
                webView.Navigate(url);
                vm.CurrentUrl = url;
            }

            vm.TelegramEnabled = settings.TelegramEnabled;
            if (settings.TelegramEnabled && settings.TelegramConfig is { } tgConfig)
            {
                vm.TelegramUserId = tgConfig.TelegramUserId;
                vm.TelegramUsername = tgConfig.Username;
                vm.TelegramFirstName = tgConfig.FirstName;
                vm.TelegramLanguageCode = tgConfig.LanguageCode;
                await _telegramService.InjectAsync(webView, tgConfig, _config.BotToken);
            }

            Logger.Information("Settings applied for {Player}: device={Device}, location={Location}, network={Network}, telegram={Tg}",
                vm.PlayerName, vm.SelectedDevice.Name,
                vm.SelectedLocation?.CityName ?? "None",
                vm.SelectedNetwork.Name, vm.TelegramEnabled);
        }
        catch (Exception ex)
        {
            Logger.Error(ex, "Failed to apply settings for {Player}", vm.PlayerName);
        }
    }

    private void OnPlayerOverlayRequested(BrowserCellViewModel vm)
    {
        if (vm.IsOverlayOpen || vm.OverlayWindow is not Window overlay)
            return;

        vm.IsOverlayOpen = true;

        var device = vm.SelectedDevice;
        overlay.Left = (SystemParameters.WorkArea.Width - device.Width - 4) / 2;
        overlay.Top = (SystemParameters.WorkArea.Height - device.Height - 40) / 2;
        overlay.Activate();
    }

    private async void OnPlayerWebViewReady(BrowserCellViewModel vm)
    {
        if (vm.WebView?.CoreWebView2 is null) return;

        _notificationService.Attach(vm.WebView.CoreWebView2, vm.PlayerId);
        _notificationService.NotificationReceived += (_, n) =>
        {
            if (n.PlayerId == vm.PlayerId)
                vm.NotificationCount++;
        };

        try
        {
            await _consoleService.AttachAsync(vm.WebView.CoreWebView2, vm.PlayerId);
            _consoleService.EntryReceived += (_, entry) =>
            {
                if (entry.PlayerId != vm.PlayerId) return;
                if (entry.Level == "error")
                {
                    vm.ConsoleErrorCount = _consoleService.GetErrorCount(vm.PlayerId);
                    vm.LastError = entry.Message;
                }
            };

            await _networkMonitorService.AttachAsync(vm.WebView.CoreWebView2, vm.PlayerId);
            _networkMonitorService.RequestFailed += (_, entry) =>
            {
                if (entry.PlayerId == vm.PlayerId)
                    vm.NetworkErrorCount++;
            };
        }
        catch (Exception ex)
        {
            Logger.Error(ex, "Failed to attach diagnostics for Player {Id}", vm.PlayerId);
        }
    }
}
