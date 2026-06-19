using System.Collections.Concurrent;
using GDD.Abstractions;
using GDD.Desktop.Engines;
using Serilog;

namespace GDD.Desktop.Services;

/// <summary>
/// Live thumbnails via periodic page.ScreenshotAsync polling. Unlike CDP screencast
/// (which is change-driven and produces nothing for static pages), polling actively
/// forces a render, so it reliably reflects the current page for any page — including
/// off-screen windows and static pages — on every platform.
/// </summary>
public sealed class PollingThumbnailService : IThumbnailService
{
    private static readonly ILogger Logger = Log.ForContext<PollingThumbnailService>();

    private readonly ConcurrentDictionary<int, CancellationTokenSource> _loops = new();

    private const int IntervalMs = 1000; // ~1 fps per player

    public void Start(int playerId, IBrowserEngine engine, Action<int, byte[]> onFrame)
    {
        var cts = new CancellationTokenSource();
        if (!_loops.TryAdd(playerId, cts)) return;
        var token = cts.Token;

        _ = Task.Run(async () =>
        {
            while (!token.IsCancellationRequested)
            {
                try
                {
                    // While the real Chromium window is restored for interaction, calling
                    // ScreenshotAsync on the visible window forces a repaint and makes it
                    // blink ~1/s. The user is looking at the real window (not the thumbnail),
                    // so pause capture until the window is parked off-screen again.
                    if (engine is not PlaywrightHeadedEngine { IsWindowHidden: false })
                    {
                        var bytes = await engine.CaptureScreenshotAsync(45);
                        if (bytes.Length > 0) onFrame(playerId, bytes);
                    }
                }
                catch (Exception ex) { Logger.Debug("Thumbnail poll failed for Player {Id}: {M}", playerId, ex.Message); }

                try { await Task.Delay(IntervalMs, token); }
                catch (OperationCanceledException) { break; }
            }
        }, token);
    }

    public void Stop(int playerId)
    {
        if (_loops.TryRemove(playerId, out var cts))
            cts.Cancel();
    }
}
