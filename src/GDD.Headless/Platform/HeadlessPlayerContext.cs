using GDD.Abstractions;
using GDD.Models;

namespace GDD.Headless.Platform;

public sealed class HeadlessPlayerContext : IPlayerContext
{
    public int PlayerId { get; }
    public string PlayerName { get; set; }
    public string CurrentUrl { get; set; }
    public string StatusText { get; set; } = "Initializing...";
    public bool IsOverlayOpen => false;
    public string NetworkStatus { get; set; } = "Online";
    public int NotificationCount { get; set; }
    public DevicePreset SelectedDevice { get; set; } = DevicePresets.Default;
    public LocationPreset? SelectedLocation { get; set; }
    public NetworkPreset SelectedNetwork { get; set; } = NetworkPresets.Online;
    public int ConsoleErrorCount { get; set; }
    public int NetworkErrorCount { get; set; }
    public string LastError { get; set; } = "";
    public string Language { get; set; } = "";
    public IBrowserEngine? Engine { get; set; }

    public HeadlessPlayerContext(int playerId, string defaultUrl)
    {
        PlayerId = playerId;
        PlayerName = $"Player {playerId}";
        CurrentUrl = defaultUrl;
    }
}
