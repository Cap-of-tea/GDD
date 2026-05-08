using System.Text.Json;
using Microsoft.Web.WebView2.Core;
using Serilog;

namespace GDD.Services;

public sealed class CdpService
{
    private static readonly ILogger Logger = Log.ForContext<CdpService>();

    public async Task CallAsync(CoreWebView2 webView, string method, object parameters)
    {
        var json = JsonSerializer.Serialize(parameters, new JsonSerializerOptions
        {
            PropertyNamingPolicy = JsonNamingPolicy.CamelCase
        });

        Logger.Debug("CDP {Method}: {Params}", method, json);
        await webView.CallDevToolsProtocolMethodAsync(method, json);
    }

    public async Task<string> CallWithResultAsync(CoreWebView2 webView, string method, object parameters)
    {
        var json = JsonSerializer.Serialize(parameters, new JsonSerializerOptions
        {
            PropertyNamingPolicy = JsonNamingPolicy.CamelCase
        });

        Logger.Debug("CDP {Method}: {Params}", method, json);
        return await webView.CallDevToolsProtocolMethodAsync(method, json);
    }
}
