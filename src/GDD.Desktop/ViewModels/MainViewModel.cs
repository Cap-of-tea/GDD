using System.Collections.ObjectModel;
using System.Collections.Specialized;
using System.Threading;
using Avalonia;
using Avalonia.Controls.ApplicationLifetimes;
using CommunityToolkit.Mvvm.ComponentModel;
using CommunityToolkit.Mvvm.Input;
using GDD;
using GDD.Abstractions;
using GDD.Desktop.Platform;
using GDD.Desktop.Views;
using GDD.Models;
using GDD.Services;
using Serilog;

namespace GDD.Desktop.ViewModels;

/// <summary>
/// Thin view-model over DesktopPlayerManager. Holds UI commands and status;
/// the manager owns player lifecycle and the observable Players collection.
/// </summary>
public partial class MainViewModel : ObservableObject
{
    private static readonly ILogger Logger = Log.ForContext<MainViewModel>();

    private readonly DesktopPlayerManager _manager;
    private readonly AppConfig _config;
    private readonly IMainThreadDispatcher _dispatcher;
    private readonly QuickAuthService _authService;
    private readonly TokenInjectionService _tokenService;
    private readonly DeviceEmulationService _deviceService;
    private readonly LocationEmulationService _locationService;
    private readonly NetworkEmulationService _networkService;
    private readonly TelegramInjectionService _telegramService;
    private readonly Services.IThumbnailService _thumbnails;

    [ObservableProperty] private string _statusText = "Ready";
    [ObservableProperty] private string _defaultUrl;
    [ObservableProperty] private string _apiEndpoint = "";
    [ObservableProperty] private bool _updateAvailable;
    [ObservableProperty] private string _updateVersion = "";

    public string Version => $"v{GddVersion.Current}";

    public ObservableCollection<DesktopPlayerContext> Players => _manager.Players;
    public int PlayerCount => Players.Count;

    public IReadOnlyList<DeviceTestPreset> Presets => DeviceTestPreset.All;

    public MainViewModel(
        DesktopPlayerManager manager,
        AppConfig config,
        IMainThreadDispatcher dispatcher,
        QuickAuthService authService,
        TokenInjectionService tokenService,
        DeviceEmulationService deviceService,
        LocationEmulationService locationService,
        NetworkEmulationService networkService,
        TelegramInjectionService telegramService,
        Services.IThumbnailService thumbnails)
    {
        _manager = manager;
        _config = config;
        _dispatcher = dispatcher;
        _authService = authService;
        _tokenService = tokenService;
        _deviceService = deviceService;
        _locationService = locationService;
        _networkService = networkService;
        _telegramService = telegramService;
        _thumbnails = thumbnails;
        _defaultUrl = config.FrontendUrl;

        Players.CollectionChanged += OnPlayersChanged;
    }

    private void OnPlayersChanged(object? sender, NotifyCollectionChangedEventArgs e)
        => OnPropertyChanged(nameof(PlayerCount));

    [RelayCommand]
    private void AddPlayer()
    {
        _manager.AddPlayers(1);
        StatusText = $"Players: {PlayerCount}";
    }

    [RelayCommand]
    private void AddPreset(DeviceTestPreset? preset)
    {
        if (preset is null) return;
        foreach (var device in preset.Devices)
            _manager.AddPlayers(1, device.Name);
        StatusText = $"Added {preset.Devices.Length} players ({preset.Name})";
    }

    [RelayCommand]
    private void RemovePlayer(DesktopPlayerContext? player)
    {
        if (player is null) return;
        _manager.RemovePlayer(player.PlayerId);
        StatusText = $"Players: {PlayerCount}";
    }

    [RelayCommand]
    private void CloseAll()
    {
        var count = PlayerCount;
        if (count == 0) return;
        foreach (var player in Players.ToList())
            _manager.RemovePlayer(player.PlayerId);
        StatusText = $"Closed {count} players";
    }

    [RelayCommand]
    private void NavigateAll()
    {
        var url = DefaultUrl?.Trim();
        if (string.IsNullOrEmpty(url)) return;
        if (!url.Contains("://")) url = "https://" + url;

        foreach (var player in Players)
        {
            player.Engine?.NavigateAsync(url);
            player.CurrentUrl = url;
        }
        StatusText = $"Navigating all to {url}";
    }

    [RelayCommand]
    private async Task QuickAuthAllAsync()
    {
        if (PlayerCount == 0) return;

        StatusText = "Quick Auth: authenticating all players...";
        var targets = Players.ToList();
        var authenticated = 0;

        foreach (var batch in targets.Chunk(8))
        {
            var tasks = batch.Select(async player =>
            {
                try
                {
                    if (player.Engine is null) return;
                    var authResult = await _authService.RegisterAndLoginAsync(player.PlayerId);
                    if (authResult is null) return;

                    await _dispatcher.InvokeAsync(async () =>
                    {
                        await _tokenService.InjectAsync(player.Engine, authResult, _config.FrontendUrl);
                    });

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
    private void OpenHelp()
    {
        var owner = MainWindow;
        var help = new HelpWindow();
        if (owner is not null) help.Show(owner);
        else help.Show();
    }

    [RelayCommand]
    private async Task OpenSettingsAsync(DesktopPlayerContext? player)
    {
        if (player is null || MainWindow is null) return;

        var dialog = new CellSettingsWindow(player, _config.FrontendUrl);
        var ok = await dialog.ShowDialog<bool>(MainWindow);
        if (!ok || player.Engine is not { } engine) return;

        try
        {
            player.SelectedDevice = dialog.SelectedDevice;
            await _deviceService.ApplyAsync(engine, dialog.SelectedDevice);

            player.SelectedLocation = dialog.SelectedLocation;
            if (dialog.SelectedLocation is not null)
                await _locationService.ApplyAsync(engine, dialog.SelectedLocation);
            else
                await _locationService.ClearAsync(engine);

            player.SelectedNetwork = dialog.SelectedNetwork;
            await _networkService.ApplyAsync(engine, dialog.SelectedNetwork);
            player.NetworkStatus = dialog.SelectedNetwork.Name;

            if (!string.IsNullOrEmpty(dialog.NavigateUrl))
            {
                var url = dialog.NavigateUrl;
                if (!url.Contains("://")) url = "https://" + url;
                await engine.NavigateAsync(url);
                player.CurrentUrl = url;
            }

            player.TelegramEnabled = dialog.TelegramEnabled;
            if (dialog.TelegramEnabled && dialog.TelegramConfig is { } tg)
            {
                player.TelegramUserId = tg.TelegramUserId;
                player.TelegramUsername = tg.Username;
                player.TelegramFirstName = tg.FirstName;
                player.TelegramLanguageCode = tg.LanguageCode;
                await _telegramService.InjectAsync(engine, tg, _config.BotToken);
            }

            StatusText = $"Settings applied: {player.PlayerName}";
        }
        catch (Exception ex)
        {
            Logger.Error(ex, "Failed to apply settings for {Player}", player.PlayerName);
            StatusText = $"Settings error: {ex.Message}";
        }
    }

    [RelayCommand]
    private void BringToFront(DesktopPlayerContext? player)
    {
        if (player?.Engine is not Engines.PlaywrightHeadedEngine engine) return;

        // Toggle the real Chromium window: off-screen ↔ on-screen for native interaction.
        if (engine.IsWindowHidden)
        {
            _ = engine.RestoreWindowAsync();
            StatusText = $"Opened {player.PlayerName} (real browser window)";
        }
        else
        {
            _ = engine.HideOffscreenAsync();
            StatusText = $"Hid {player.PlayerName}";
        }
    }

    private static Avalonia.Controls.Window? MainWindow
        => (Application.Current?.ApplicationLifetime as IClassicDesktopStyleApplicationLifetime)?.MainWindow;
}
