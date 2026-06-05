using GDD.Abstractions;

namespace GDD.Desktop.Services;

/// <summary>Produces live thumbnail bitmaps for a player's browser.</summary>
public interface IThumbnailService
{
    void Start(int playerId, IBrowserEngine engine, Action<int, byte[]> onFrame);
    void Stop(int playerId);
}
