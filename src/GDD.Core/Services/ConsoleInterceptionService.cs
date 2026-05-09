using System.Collections.Concurrent;
using System.Text.Json;
using GDD.Abstractions;
using GDD.Collections;
using GDD.Models;
using Serilog;

namespace GDD.Services;

public sealed class ConsoleInterceptionService
{
    private static readonly ILogger Logger = Log.ForContext<ConsoleInterceptionService>();
    private readonly CdpService _cdp;
    private readonly ConcurrentDictionary<int, RingBuffer<ConsoleEntry>> _buffers = new();

    public event EventHandler<ConsoleEntry>? EntryReceived;

    public ConsoleInterceptionService(CdpService cdp)
    {
        _cdp = cdp;
    }

    public async Task AttachAsync(IBrowserEngine engine, int playerId)
    {
        _buffers.TryAdd(playerId, new RingBuffer<ConsoleEntry>());

        await _cdp.CallAsync(engine, "Runtime.enable", new { });

        var consoleSub = engine.SubscribeToCdpEvent("Runtime.consoleAPICalled");
        consoleSub.EventReceived += (_, json) =>
        {
            try
            {
                using var doc = JsonDocument.Parse(json);
                var root = doc.RootElement;

                var level = root.GetProperty("type").GetString() ?? "log";
                var argsArray = root.GetProperty("args");
                var messageParts = new List<string>();
                foreach (var arg in argsArray.EnumerateArray())
                {
                    if (arg.TryGetProperty("value", out var val))
                        messageParts.Add(val.ToString());
                    else if (arg.TryGetProperty("description", out var desc))
                        messageParts.Add(desc.GetString() ?? "");
                    else if (arg.TryGetProperty("type", out var type))
                        messageParts.Add($"[{type.GetString()}]");
                }

                var stackTrace = "";
                if (root.TryGetProperty("stackTrace", out var st) &&
                    st.TryGetProperty("callFrames", out var frames) &&
                    frames.GetArrayLength() > 0)
                {
                    var frame = frames[0];
                    stackTrace = $"{frame.GetProperty("functionName").GetString()} at {frame.GetProperty("url").GetString()}:{frame.GetProperty("lineNumber").GetInt32()}";
                }

                var entry = new ConsoleEntry
                {
                    PlayerId = playerId,
                    Level = level,
                    Message = string.Join(" ", messageParts),
                    Source = stackTrace.Length > 0 ? stackTrace : null,
                    Timestamp = DateTimeOffset.Now
                };

                AddEntry(playerId, entry);
            }
            catch (Exception ex)
            {
                Logger.Warning(ex, "Failed to parse console event for Player {Id}", playerId);
            }
        };

        var exceptionSub = engine.SubscribeToCdpEvent("Runtime.exceptionThrown");
        exceptionSub.EventReceived += (_, json) =>
        {
            try
            {
                using var doc = JsonDocument.Parse(json);
                var root = doc.RootElement;
                var exDetail = root.GetProperty("exceptionDetails");

                var message = "Uncaught exception";
                if (exDetail.TryGetProperty("exception", out var exc) &&
                    exc.TryGetProperty("description", out var descEl))
                    message = descEl.GetString() ?? message;
                else if (exDetail.TryGetProperty("text", out var textEl))
                    message = textEl.GetString() ?? message;

                string? stack = null;
                if (exDetail.TryGetProperty("stackTrace", out var st) &&
                    st.TryGetProperty("callFrames", out var frames2))
                {
                    var lines = new List<string>();
                    foreach (var frame in frames2.EnumerateArray())
                    {
                        var fn = frame.GetProperty("functionName").GetString();
                        var url = frame.GetProperty("url").GetString();
                        var line = frame.GetProperty("lineNumber").GetInt32();
                        lines.Add($"  at {fn} ({url}:{line})");
                    }
                    stack = string.Join("\n", lines);
                }

                int? lineNum = exDetail.TryGetProperty("lineNumber", out var ln) ? ln.GetInt32() : null;
                int? colNum = exDetail.TryGetProperty("columnNumber", out var cn) ? cn.GetInt32() : null;

                var entry = new ConsoleEntry
                {
                    PlayerId = playerId,
                    Level = "error",
                    Message = message,
                    LineNumber = lineNum,
                    ColumnNumber = colNum,
                    StackTrace = stack,
                    IsException = true,
                    Timestamp = DateTimeOffset.Now
                };

                AddEntry(playerId, entry);
            }
            catch (Exception ex)
            {
                Logger.Warning(ex, "Failed to parse exception event for Player {Id}", playerId);
            }
        };

        Logger.Information("Console interception attached for Player {Id}", playerId);
    }

    private void AddEntry(int playerId, ConsoleEntry entry)
    {
        if (_buffers.TryGetValue(playerId, out var buffer))
            buffer.Add(entry);

        EntryReceived?.Invoke(this, entry);
    }

    public List<ConsoleEntry> GetEntries(int playerId, string? levelFilter = null)
    {
        if (!_buffers.TryGetValue(playerId, out var buffer))
            return [];

        var entries = buffer.ToList();
        if (!string.IsNullOrEmpty(levelFilter))
            entries = entries.Where(e => e.Level.Equals(levelFilter, StringComparison.OrdinalIgnoreCase)).ToList();
        return entries;
    }

    public int GetErrorCount(int playerId)
    {
        if (!_buffers.TryGetValue(playerId, out var buffer))
            return 0;
        return buffer.ToList().Count(e => e.Level == "error");
    }

    public void Clear(int playerId)
    {
        if (_buffers.TryGetValue(playerId, out var buffer))
            buffer.Clear();
    }

    public void Remove(int playerId)
    {
        _buffers.TryRemove(playerId, out _);
    }
}
