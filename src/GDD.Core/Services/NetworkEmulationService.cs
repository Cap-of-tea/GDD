using GDD.Abstractions;
using GDD.Models;
using Serilog;

namespace GDD.Services;

public sealed class NetworkEmulationService
{
    private static readonly ILogger Logger = Log.ForContext<NetworkEmulationService>();
    private readonly CdpService _cdp;

    public NetworkEmulationService(CdpService cdp)
    {
        _cdp = cdp;
    }

    public async Task ApplyAsync(IBrowserEngine engine, NetworkPreset preset)
    {
        await _cdp.CallAsync(engine, "Network.enable", new { });

        await _cdp.CallAsync(engine, "Network.emulateNetworkConditions", new
        {
            offline = preset.Offline,
            latency = preset.LatencyMs,
            downloadThroughput = preset.DownloadThroughputBps,
            uploadThroughput = preset.UploadThroughputBps
        });

        Logger.Information("Network set to {Preset} (offline={Offline}, latency={Latency}ms)",
            preset.Name, preset.Offline, preset.LatencyMs);
    }

    public async Task ClearAsync(IBrowserEngine engine)
    {
        await _cdp.CallAsync(engine, "Network.emulateNetworkConditions", new
        {
            offline = false,
            latency = 0,
            downloadThroughput = -1,
            uploadThroughput = -1
        });
    }
}
