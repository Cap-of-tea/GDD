using System.Collections.Concurrent;
using System.Text.Json;
using Microsoft.Web.WebView2.Core;
using GDD.Collections;
using GDD.Models;
using Serilog;

namespace GDD.Services;

public sealed class NetworkMonitoringService
{
    private static readonly ILogger Logger = Log.ForContext<NetworkMonitoringService>();
    private readonly CdpService _cdp;
    private readonly ConcurrentDictionary<int, RingBuffer<NetworkEntry>> _buffers = new();
    private readonly ConcurrentDictionary<string, NetworkEntry> _pending = new();

    public event EventHandler<NetworkEntry>? RequestCompleted;
    public event EventHandler<NetworkEntry>? RequestFailed;

    public NetworkMonitoringService(CdpService cdp)
    {
        _cdp = cdp;
    }

    public async Task AttachAsync(CoreWebView2 webView, int playerId)
    {
        _buffers.TryAdd(playerId, new RingBuffer<NetworkEntry>());

        await _cdp.CallAsync(webView, "Network.enable", new { });

        webView.GetDevToolsProtocolEventReceiver("Network.requestWillBeSent")
            .DevToolsProtocolEventReceived += (_, e) =>
        {
            try
            {
                using var doc = JsonDocument.Parse(e.ParameterObjectAsJson);
                var root = doc.RootElement;

                var requestId = root.GetProperty("requestId").GetString() ?? "";
                var request = root.GetProperty("request");
                var entry = new NetworkEntry
                {
                    PlayerId = playerId,
                    RequestId = requestId,
                    Method = request.GetProperty("method").GetString() ?? "GET",
                    Url = request.GetProperty("url").GetString() ?? "",
                    ResourceType = root.TryGetProperty("type", out var typeEl) ? typeEl.GetString() : null,
                    RequestTime = DateTimeOffset.Now
                };

                _pending[requestId] = entry;
            }
            catch (Exception ex)
            {
                Logger.Warning(ex, "Failed to parse Network.requestWillBeSent for Player {Id}", playerId);
            }
        };

        webView.GetDevToolsProtocolEventReceiver("Network.responseReceived")
            .DevToolsProtocolEventReceived += (_, e) =>
        {
            try
            {
                using var doc = JsonDocument.Parse(e.ParameterObjectAsJson);
                var root = doc.RootElement;

                var requestId = root.GetProperty("requestId").GetString() ?? "";
                if (!_pending.TryGetValue(requestId, out var entry)) return;

                var response = root.GetProperty("response");
                entry.StatusCode = response.GetProperty("status").GetInt32();
                entry.StatusText = response.TryGetProperty("statusText", out var st) ? st.GetString() : null;
                entry.MimeType = response.TryGetProperty("mimeType", out var mt) ? mt.GetString() : null;
                entry.ResponseTime = DateTimeOffset.Now;
                entry.DurationMs = (entry.ResponseTime.Value - entry.RequestTime).TotalMilliseconds;

                if (response.TryGetProperty("headers", out var headers) &&
                    headers.TryGetProperty("content-length", out var cl) &&
                    long.TryParse(cl.GetString(), out var contentLength))
                    entry.ContentLength = contentLength;
            }
            catch (Exception ex)
            {
                Logger.Warning(ex, "Failed to parse Network.responseReceived for Player {Id}", playerId);
            }
        };

        webView.GetDevToolsProtocolEventReceiver("Network.loadingFinished")
            .DevToolsProtocolEventReceived += (_, e) =>
        {
            try
            {
                using var doc = JsonDocument.Parse(e.ParameterObjectAsJson);
                var requestId = doc.RootElement.GetProperty("requestId").GetString() ?? "";

                if (!_pending.TryRemove(requestId, out var entry)) return;

                entry.Completed = true;
                if (entry.DurationMs is null)
                    entry.DurationMs = (DateTimeOffset.Now - entry.RequestTime).TotalMilliseconds;

                if (_buffers.TryGetValue(playerId, out var buffer))
                    buffer.Add(entry);

                RequestCompleted?.Invoke(this, entry);
            }
            catch (Exception ex)
            {
                Logger.Warning(ex, "Failed to parse Network.loadingFinished for Player {Id}", playerId);
            }
        };

        webView.GetDevToolsProtocolEventReceiver("Network.loadingFailed")
            .DevToolsProtocolEventReceived += (_, e) =>
        {
            try
            {
                using var doc = JsonDocument.Parse(e.ParameterObjectAsJson);
                var root = doc.RootElement;
                var requestId = root.GetProperty("requestId").GetString() ?? "";

                if (!_pending.TryRemove(requestId, out var entry)) return;

                entry.Failed = true;
                entry.Completed = true;
                entry.ErrorText = root.TryGetProperty("errorText", out var et) ? et.GetString() : "Unknown error";
                entry.DurationMs = (DateTimeOffset.Now - entry.RequestTime).TotalMilliseconds;

                if (_buffers.TryGetValue(playerId, out var buffer))
                    buffer.Add(entry);

                RequestFailed?.Invoke(this, entry);
            }
            catch (Exception ex)
            {
                Logger.Warning(ex, "Failed to parse Network.loadingFailed for Player {Id}", playerId);
            }
        };

        Logger.Information("Network monitoring attached for Player {Id}", playerId);
    }

    public List<NetworkEntry> GetEntries(int playerId, bool failedOnly = false, string? resourceTypeFilter = null)
    {
        if (!_buffers.TryGetValue(playerId, out var buffer))
            return [];

        var entries = buffer.ToList();
        if (failedOnly)
            entries = entries.Where(e => e.Failed).ToList();
        if (!string.IsNullOrEmpty(resourceTypeFilter))
            entries = entries.Where(e =>
                e.ResourceType?.Equals(resourceTypeFilter, StringComparison.OrdinalIgnoreCase) == true).ToList();
        return entries;
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
