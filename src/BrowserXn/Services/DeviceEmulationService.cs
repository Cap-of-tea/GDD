using Microsoft.Web.WebView2.Core;
using GDD.Models;
using Serilog;

namespace GDD.Services;

public sealed class DeviceEmulationService
{
    private static readonly ILogger Logger = Log.ForContext<DeviceEmulationService>();
    private readonly CdpService _cdp;

    public DeviceEmulationService(CdpService cdp)
    {
        _cdp = cdp;
    }

    public async Task ApplyAsync(CoreWebView2 webView, DevicePreset preset)
    {
        await _cdp.CallAsync(webView, "Emulation.setDeviceMetricsOverride", new
        {
            width = preset.Width,
            height = preset.Height,
            deviceScaleFactor = preset.DeviceScaleFactor,
            mobile = preset.IsMobile
        });

        await _cdp.CallAsync(webView, "Emulation.setUserAgentOverride", new
        {
            userAgent = preset.UserAgent
        });

        await _cdp.CallAsync(webView, "Emulation.setTouchEmulationEnabled", new
        {
            enabled = preset.HasTouch,
            maxTouchPoints = 5
        });

        Logger.Information("Device emulation set to {Device} ({W}x{H})",
            preset.Name, preset.Width, preset.Height);
    }

    public async Task ClearAsync(CoreWebView2 webView)
    {
        await _cdp.CallAsync(webView, "Emulation.clearDeviceMetricsOverride", new { });
        await _cdp.CallAsync(webView, "Emulation.setTouchEmulationEnabled", new { enabled = false });
    }
}
