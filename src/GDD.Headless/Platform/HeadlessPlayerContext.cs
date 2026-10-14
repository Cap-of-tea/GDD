using GDD.Abstractions;
using GDD.Models;

namespace GDD.Headless.Platform;

public sealed class HeadlessPlayerContext : IPlayerContext
{
    private readonly TaskCompletionSource _engineTcs = new(TaskCreationOptions.RunContinuationsAsynchronously);

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
    public string? OwnerSessionId { get; set; }

    /// <summary>
    /// Last time a tool acted on this player (set on creation and whenever the player is
    /// resolved for a tool). The idle reaper removes players untouched for too long, which is
    /// the only cleanup streamable-HTTP sessions get — they have no disconnect event.
    /// </summary>
    public DateTime LastActivityUtc { get; set; } = DateTime.UtcNow;

    public IBrowserEngine? Engine { get; set; }
    public Task EngineReady => _engineTcs.Task;

    public void SetEngineReady() => _engineTcs.TrySetResult();
    public void SetEngineFailed() => _engineTcs.TrySetResult();

    public HeadlessPlayerContext(int playerId, string defaultUrl)
    {
        PlayerId = playerId;
        PlayerName = $"Player {playerId}";
        CurrentUrl = defaultUrl;
    }
}
