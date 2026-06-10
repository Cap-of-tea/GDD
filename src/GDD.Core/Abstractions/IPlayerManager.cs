namespace GDD.Abstractions;

public interface IPlayerManager
{
    IReadOnlyList<IPlayerContext> GetPlayers();
    IPlayerContext? GetPlayer(int playerId);
    IReadOnlyList<int> AddPlayers(int count, string? devicePreset = null, string? sessionId = null);
    void RemovePlayer(int playerId);
}

public static class PlayerManagerExtensions
{
    public static async Task<IPlayerContext?> GetReadyPlayerAsync(this IPlayerManager manager, int playerId)
    {
        var player = manager.GetPlayer(playerId);
        if (player is null) return null;
        if (player.Engine is not null) return player;
        using var cts = new CancellationTokenSource(10_000);
        try { await player.EngineReady.WaitAsync(cts.Token); }
        catch (OperationCanceledException) { }
        return player.Engine is not null ? player : null;
    }
}
