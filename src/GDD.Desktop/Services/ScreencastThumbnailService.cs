using System.Collections.Concurrent;
using GDD.Abstractions;
using GDD.Desktop.Engines;
using GDD.Models;
using Serilog;

namespace GDD.Desktop.Services;

/// <summary>
/// Live thumbnails via CDP Page.startScreencast. Bridges the engine's
/// ScreencastFrame (raw JPEG bytes) to a per-player callback.
/// </summary>
public sealed class ScreencastThumbnailService : IThumbnailService
{
    private static readonly ILogger Logger = Log.ForContext<ScreencastThumbnailService>();

    private sealed record Subscription(PlaywrightHeadedEngine Engine, EventHandler<byte[]> Handler);

    private readonly AppConfig _config;
    private readonly ConcurrentDictionary<int, Subscription> _subs = new();

    public ScreencastThumbnailService(AppConfig config) => _config = config;

    public void Start(int playerId, IBrowserEngine engine, Action<int, byte[]> onFrame)
    {
        if (engine is not PlaywrightHeadedEngine headed) return;

        EventHandler<byte[]> handler = (_, bytes) => onFrame(playerId, bytes);
        if (!_subs.TryAdd(playerId, new Subscription(headed, handler))) return;

        headed.ScreencastFrame += handler;
        // Cap frame size to keep thumbnails cheap; ~12 fps via everyNthFrame.
        _ = headed.StartScreencastAsync(quality: 50, everyNthFrame: 2, maxWidth: 480, maxHeight: 900)
            .ContinueWith(t =>
            {
                if (t.IsFaulted)
                    Logger.Warning(t.Exception, "Screencast start failed for Player {Id}", playerId);
            });
    }

    public void Stop(int playerId)
    {
        if (!_subs.TryRemove(playerId, out var sub)) return;
        sub.Engine.ScreencastFrame -= sub.Handler;
        _ = sub.Engine.StopScreencastAsync();
    }
}
