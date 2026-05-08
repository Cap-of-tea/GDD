using Microsoft.Web.WebView2.Core;
using GDD.Models;
using Serilog;

namespace GDD.Services;

public sealed class LocationEmulationService
{
    private static readonly ILogger Logger = Log.ForContext<LocationEmulationService>();
    private readonly CdpService _cdp;

    public LocationEmulationService(CdpService cdp)
    {
        _cdp = cdp;
    }

    public async Task ApplyAsync(CoreWebView2 webView, LocationPreset preset)
    {
        await _cdp.CallAsync(webView, "Emulation.setGeolocationOverride", new
        {
            latitude = preset.Latitude,
            longitude = preset.Longitude,
            accuracy = preset.Accuracy
        });

        await _cdp.CallAsync(webView, "Emulation.setTimezoneOverride", new
        {
            timezoneId = preset.TimezoneId
        });

        await _cdp.CallAsync(webView, "Emulation.setLocaleOverride", new
        {
            locale = preset.Locale
        });

        Logger.Information("Location set to {City} ({Lat}, {Lon}) TZ={TZ} Locale={Locale}",
            preset.CityName, preset.Latitude, preset.Longitude, preset.TimezoneId, preset.Locale);
    }

    public async Task ClearAsync(CoreWebView2 webView)
    {
        await _cdp.CallAsync(webView, "Emulation.clearGeolocationOverride", new { });
    }
}
