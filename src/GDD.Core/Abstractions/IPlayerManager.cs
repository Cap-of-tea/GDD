namespace GDD.Abstractions;

public interface IPlayerManager
{
    IReadOnlyList<IPlayerContext> GetPlayers();
    IPlayerContext? GetPlayer(int playerId);
    IReadOnlyList<int> AddPlayers(int count, string? devicePreset = null);
    void RemovePlayer(int playerId);
}
