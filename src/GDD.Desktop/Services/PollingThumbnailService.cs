using System.Collections.Concurrent;
using GDD.Abstractions;
using Serilog;

namespace GDD.Desktop.Services;

/// <summary>
/// Live thumbnails via periodic page.ScreenshotAsync polling. Unlike CDP screencast
/// (which is change-driven and produces nothing for static pages), polling reliably
/// reflects the current page state for any page, in headless mode, on every platform.
/// A focused player (overlay open) is polled faster for a responsive interactive view.
/// </summary>
public sealed class PollingThumbnailService : IThumbnailService
{
    private static readonly ILogger Logger = Log.ForContext<PollingThumbnailService>();

    private sealed class Loop
    {
        public required IBrowserEngine Engine;
        public required CancellationTokenSource Cts;
        public volatile int IntervalMs;
    }

    private readonly ConcurrentDictionary<int, Loop> _loops = new();

    private const int GridIntervalMs = 1000;   // ~1 fps per player in the grid
    private const int FocusIntervalMs = 120;   // ~8 fps for the focused/overlay player

    public void Start(int playerId, IBrowserEngine engine, Action<int, byte[]> onFrame)
    {
        var loop = new Loop { Engine = engine, Cts = new CancellationTokenSource(), IntervalMs = GridIntervalMs };
        if (!_loops.TryAdd(playerId, loop)) return;

        _ = Task.Run(async () =>
        {
            var token = loop.Cts.Token;
            while (!token.IsCancellationRequested)
            {
                try
                {
                    var bytes = await engine.CaptureScreenshotAsync(45);
                    if (bytes.Length > 0) onFrame(playerId, bytes);
                }
                catch (Exception ex) { Logger.Debug("Thumbnail poll failed for Player {Id}: {M}", playerId, ex.Message); }

                try { await Task.Delay(loop.IntervalMs, token); }
                catch (OperationCanceledException) { break; }
            }
        }, loop.Cts.Token);
    }

    /// <summary>Raise the poll rate for a player whose interactive overlay is open.</summary>
    public void SetFocused(int playerId, bool focused)
    {
        if (_loops.TryGetValue(playerId, out var loop))
            loop.IntervalMs = focused ? FocusIntervalMs : GridIntervalMs;
    }

    public void Stop(int playerId)
    {
        if (_loops.TryRemove(playerId, out var loop))
            loop.Cts.Cancel();
    }
}
