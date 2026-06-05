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
        TokenInjectionService tokenService)
    {
        _manager = manager;
        _config = config;
        _dispatcher = dispatcher;
        _authService = authService;
        _tokenService = tokenService;
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

    // Stubs wired in Этап 4.
    [RelayCommand]
    private void OpenSettings(DesktopPlayerContext? player)
    {
        if (player is null) return;
        StatusText = $"Settings for {player.PlayerName} (coming in Этап 4)";
    }

    [RelayCommand]
    private void BringToFront(DesktopPlayerContext? player)
    {
        if (player is null) return;
        StatusText = $"Focus {player.PlayerName}";
    }

    private static Avalonia.Controls.Window? MainWindow
        => (Application.Current?.ApplicationLifetime as IClassicDesktopStyleApplicationLifetime)?.MainWindow;
}
