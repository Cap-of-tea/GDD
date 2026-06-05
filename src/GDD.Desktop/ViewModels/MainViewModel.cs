using System.Collections.ObjectModel;
using CommunityToolkit.Mvvm.ComponentModel;
using CommunityToolkit.Mvvm.Input;
using GDD;
using GDD.Desktop.Platform;
using GDD.Models;

namespace GDD.Desktop.ViewModels;

/// <summary>
/// Thin view-model over DesktopPlayerManager. Holds UI commands and status;
/// the manager owns player lifecycle and the observable Players collection.
/// </summary>
public partial class MainViewModel : ObservableObject
{
    private readonly DesktopPlayerManager _manager;

    [ObservableProperty] private string _statusText = "Ready";
    [ObservableProperty] private string _defaultUrl;
    [ObservableProperty] private string _apiEndpoint = "";

    public string Version => $"v{GddVersion.Current}";

    public ObservableCollection<DesktopPlayerContext> Players => _manager.Players;

    public MainViewModel(DesktopPlayerManager manager, AppConfig config)
    {
        _manager = manager;
        _defaultUrl = config.FrontendUrl;
    }

    [RelayCommand]
    private void AddPlayer()
    {
        _manager.AddPlayers(1);
        StatusText = $"Players: {Players.Count}";
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
    private void CloseAll()
    {
        var count = Players.Count;
        if (count == 0) return;
        foreach (var player in Players.ToList())
            _manager.RemovePlayer(player.PlayerId);
        StatusText = $"Closed {count} players";
    }
}
