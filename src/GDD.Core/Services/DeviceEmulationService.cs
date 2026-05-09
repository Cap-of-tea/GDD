using GDD.Abstractions;
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

    public async Task ApplyAsync(IBrowserEngine engine, DevicePreset preset)
    {
        await _cdp.CallAsync(engine, "Emulation.setDeviceMetricsOverride", new
        {
            width = preset.Width,
            height = preset.Height,
            deviceScaleFactor = preset.DeviceScaleFactor,
            mobile = preset.IsMobile
        });

        await _cdp.CallAsync(engine, "Emulation.setUserAgentOverride", new
        {
            userAgent = preset.UserAgent
        });

        await _cdp.CallAsync(engine, "Emulation.setTouchEmulationEnabled", new
        {
            enabled = preset.HasTouch,
            maxTouchPoints = 5
        });

        Logger.Information("Device emulation set to {Device} ({W}x{H})",
            preset.Name, preset.Width, preset.Height);
    }

    public async Task ClearAsync(IBrowserEngine engine)
    {
        await _cdp.CallAsync(engine, "Emulation.clearDeviceMetricsOverride", new { });
        await _cdp.CallAsync(engine, "Emulation.setTouchEmulationEnabled", new { enabled = false });
    }
}
