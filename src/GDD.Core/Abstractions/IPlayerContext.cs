using GDD.Models;

namespace GDD.Abstractions;

public interface IPlayerContext
{
    int PlayerId { get; }
    string PlayerName { get; }
    string CurrentUrl { get; set; }
    string StatusText { get; set; }
    bool IsOverlayOpen { get; }
    string NetworkStatus { get; }
    int NotificationCount { get; set; }
    DevicePreset SelectedDevice { get; set; }
    LocationPreset? SelectedLocation { get; set; }
    NetworkPreset SelectedNetwork { get; set; }
    int ConsoleErrorCount { get; set; }
    int NetworkErrorCount { get; set; }
    string LastError { get; set; }
    string Language { get; set; }
    IBrowserEngine? Engine { get; }
}
