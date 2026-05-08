namespace GDD.Models;

public sealed record NetworkPreset(
    string Name,
    bool Offline,
    int LatencyMs,
    long DownloadThroughputBps,
    long UploadThroughputBps);

public static class NetworkPresets
{
    public static readonly NetworkPreset Online = new("Online", false, 0, -1, -1);
    public static readonly NetworkPreset FourG = new("4G", false, 20, 4_000_000, 3_000_000);
    public static readonly NetworkPreset Fast3G = new("Fast 3G", false, 563, 1_600_000, 768_000);
    public static readonly NetworkPreset Slow3G = new("Slow 3G", false, 2000, 500_000, 500_000);
    public static readonly NetworkPreset OfflinePreset = new("Offline", true, 0, 0, 0);

    public static IReadOnlyList<NetworkPreset> All { get; } = new[]
    {
        Online, FourG, Fast3G, Slow3G, OfflinePreset
    };
}
