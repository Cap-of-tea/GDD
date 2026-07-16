using System.Collections.Concurrent;
using System.Text;
using System.Text.Json;
using GDD.Abstractions;
using Serilog;

namespace GDD.Services;

/// <summary>What to do with intercepted responses for one player.</summary>
public sealed class InterceptionRules
{
    /// <summary>Strip X-Frame-Options and the CSP frame-ancestors directive so the page can be framed.</summary>
    public bool AllowFraming { get; init; }

    /// <summary>Response header names to remove (case-insensitive).</summary>
    public IReadOnlyList<string> StripResponseHeaders { get; init; } = [];

    /// <summary>Response headers to add/replace.</summary>
    public IReadOnlyDictionary<string, string> SetResponseHeaders { get; init; } =
        new Dictionary<string, string>();

    /// <summary>CDP url pattern the interception applies to.</summary>
    public string UrlPattern { get; init; } = "*";

    public bool IsNoop => !AllowFraming && StripResponseHeaders.Count == 0 && SetResponseHeaders.Count == 0;
}

/// <summary>
/// Rewrites response headers via CDP Fetch, engine-agnostically (verified on both the
/// Playwright engines and WebView2).
///
/// Hard-won details, do not "simplify" without re-testing:
/// * <c>Fetch.continueResponse</c> reports success but Chromium ignores the modified
///   headers — the only thing that actually works is getResponseBody + fulfillRequest.
/// * fulfillRequest needs the decoded body, so content-encoding/content-length MUST be
///   dropped or the response is corrupt.
/// * Fetch is a BLOCKING domain: every matched request stalls until we answer, so every
///   path here must resolve the request exactly once — hence the fail-open finally.
/// * Scoped to Document responses only: that keeps media/XHR out of the buffer and the
///   blast radius small.
/// </summary>
public sealed class RequestInterceptionService
{
    private static readonly ILogger Logger = Log.ForContext<RequestInterceptionService>();

    /// <summary>Cap on how long one paused request may wait on us before we let it through.</summary>
    private const int BodyTimeoutMs = 5000;

    private readonly CdpService _cdp;
    private readonly ConcurrentDictionary<int, PlayerState> _byPlayer = new();

    private sealed class PlayerState
    {
        public required IBrowserEngine Engine { get; init; }
        public required InterceptionRules Rules { get; set; }
        public ICdpEventSubscription? Subscription { get; set; }
    }

    public RequestInterceptionService(CdpService cdp) => _cdp = cdp;

    public InterceptionRules? GetRules(int playerId) =>
        _byPlayer.TryGetValue(playerId, out var s) ? s.Rules : null;

    public async Task ApplyAsync(IBrowserEngine engine, int playerId, InterceptionRules rules)
    {
        if (rules.IsNoop)
        {
            await DisableAsync(engine, playerId);
            return;
        }

        // Re-applying: keep one subscription per player, just swap the rules.
        if (_byPlayer.TryGetValue(playerId, out var existing))
        {
            existing.Rules = rules;
            await _cdp.CallAsync(engine, "Fetch.enable", BuildEnableParams(rules));
            Logger.Information("Interception rules updated for Player {Id}", playerId);
            return;
        }

        var state = new PlayerState { Engine = engine, Rules = rules };
        _byPlayer[playerId] = state;

        var sub = engine.SubscribeToCdpEvent("Fetch.requestPaused");
        state.Subscription = sub;
        sub.EventReceived += (_, json) => _ = HandlePausedAsync(playerId, json);

        await _cdp.CallAsync(engine, "Fetch.enable", BuildEnableParams(rules));
        Logger.Information("Interception enabled for Player {Id} (pattern {Pattern}, allowFraming={Framing})",
            playerId, rules.UrlPattern, rules.AllowFraming);
    }

    public async Task DisableAsync(IBrowserEngine engine, int playerId)
    {
        if (!_byPlayer.TryRemove(playerId, out var state)) return;

        try { await _cdp.CallAsync(engine, "Fetch.disable", new { }); }
        catch (Exception ex) { Logger.Warning(ex, "Fetch.disable failed for Player {Id}", playerId); }

        state.Subscription?.Dispose();
        Logger.Information("Interception disabled for Player {Id}", playerId);
    }

    public void Remove(int playerId) => _byPlayer.TryRemove(playerId, out _);

    private static object BuildEnableParams(InterceptionRules rules) => new
    {
        patterns = new[]
        {
            new
            {
                urlPattern = rules.UrlPattern,
                resourceType = "Document",   // CDP has no "SubDocument"; iframes are Documents
                requestStage = "Response"
            }
        }
    };

    private async Task HandlePausedAsync(int playerId, string json)
    {
        if (!_byPlayer.TryGetValue(playerId, out var state)) return;

        var engine = state.Engine;
        var requestId = "";
        var resolved = false;

        try
        {
            using var doc = JsonDocument.Parse(json);
            var root = doc.RootElement;
            requestId = root.GetProperty("requestId").GetString() ?? "";
            if (requestId.Length == 0) return;

            // Request stage (shouldn't happen with our pattern) — let it go.
            if (!root.TryGetProperty("responseStatusCode", out JsonElement statusEl))
                return;

            var status = statusEl.GetInt32();

            // Redirects carry no usable body; rewriting them breaks the chain.
            if (status is >= 300 and < 400)
                return;

            var headers = RewriteHeaders(root, state.Rules);

            var bodyTask = _cdp.CallWithResultAsync(engine, "Fetch.getResponseBody", new { requestId });
            if (await Task.WhenAny(bodyTask, Task.Delay(BodyTimeoutMs)) != bodyTask)
            {
                Logger.Warning("getResponseBody timed out for Player {Id}; passing through", playerId);
                return;
            }

            using var bodyDoc = JsonDocument.Parse(await bodyTask);
            var body = bodyDoc.RootElement.GetProperty("body").GetString() ?? "";
            var isBase64 = bodyDoc.RootElement.GetProperty("base64Encoded").GetBoolean();
            var base64Body = isBase64 ? body : Convert.ToBase64String(Encoding.UTF8.GetBytes(body));

            await _cdp.CallAsync(engine, "Fetch.fulfillRequest", new
            {
                requestId,
                responseCode = status,
                responseHeaders = headers,
                body = base64Body
            });
            resolved = true;
        }
        catch (Exception ex)
        {
            Logger.Warning(ex, "Interception failed for Player {Id}; passing request through", playerId);
        }
        finally
        {
            // Fetch is blocking: an unanswered request hangs the page forever.
            if (!resolved && requestId.Length > 0)
            {
                try { await _cdp.CallAsync(engine, "Fetch.continueRequest", new { requestId }); }
                catch (Exception ex) { Logger.Debug(ex, "continueRequest failed for Player {Id}", playerId); }
            }
        }
    }

    private static List<object> RewriteHeaders(JsonElement root, InterceptionRules rules)
    {
        var strip = new HashSet<string>(rules.StripResponseHeaders, StringComparer.OrdinalIgnoreCase)
        {
            // fulfillRequest re-sends a decoded body — keeping these corrupts it.
            "content-encoding",
            "content-length"
        };
        if (rules.AllowFraming) strip.Add("x-frame-options");

        var headers = new List<object>();

        if (root.TryGetProperty("responseHeaders", out var hs) && hs.ValueKind == JsonValueKind.Array)
        {
            foreach (var h in hs.EnumerateArray())
            {
                var name = h.GetProperty("name").GetString() ?? "";
                var value = h.TryGetProperty("value", out var v) ? v.GetString() ?? "" : "";

                if (strip.Contains(name)) continue;
                if (rules.SetResponseHeaders.ContainsKey(name)) continue; // replaced below

                if (rules.AllowFraming && name.Equals("content-security-policy", StringComparison.OrdinalIgnoreCase))
                {
                    value = StripFrameAncestors(value);
                    if (value.Length == 0) continue;   // CSP was only frame-ancestors
                }

                headers.Add(new { name, value });
            }
        }

        foreach (var kv in rules.SetResponseHeaders)
            headers.Add(new { name = kv.Key, value = kv.Value });

        return headers;
    }

    /// <summary>Removes only the frame-ancestors directive, leaving the rest of the policy
    /// intact — nuking the whole CSP would mask real problems in the page under test.</summary>
    internal static string StripFrameAncestors(string csp) =>
        string.Join("; ", csp
            .Split(';', StringSplitOptions.RemoveEmptyEntries | StringSplitOptions.TrimEntries)
            .Where(d => !d.StartsWith("frame-ancestors", StringComparison.OrdinalIgnoreCase)));
}
