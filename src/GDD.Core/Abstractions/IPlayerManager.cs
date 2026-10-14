namespace GDD.Abstractions;

public interface IPlayerManager
{
    IReadOnlyList<IPlayerContext> GetPlayers();
    IPlayerContext? GetPlayer(int playerId);
    IReadOnlyList<int> AddPlayers(int count, string? devicePreset = null, string? sessionId = null);
    void RemovePlayer(int playerId);

    /// <summary>
    /// Switch a live player to a device preset by rebuilding its browser context, and report
    /// whether that was done. Playwright fixes per-context emulation (viewport, scale factor,
    /// mobile/touch, UA, screen) at creation, so CDP overlays alone leave a player half-switched
    /// after a reload — only a fresh context changes the device cleanly. Default: not handled
    /// (the caller falls back to the CDP-overlay path). Headless overrides it; rebuilding the
    /// context starts a fresh session (cookies and storage are cleared).
    /// </summary>
    Task<bool> TrySwitchDeviceAsync(int playerId, string presetName) => Task.FromResult(false);
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
