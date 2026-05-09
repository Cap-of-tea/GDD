namespace GDD.Abstractions;

public interface IPlayerManager
{
    IReadOnlyList<IPlayerContext> GetPlayers();
    IPlayerContext? GetPlayer(int playerId);
    IReadOnlyList<int> AddPlayers(int count);
    void RemovePlayer(int playerId);
}
