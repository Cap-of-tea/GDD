using System.Text.RegularExpressions;

namespace GDD.Services;

/// <summary>
/// Builds a coherent Chromium <c>userAgentMetadata</c> object (Client Hints) from a UA string,
/// for CDP <c>Emulation.setUserAgentOverride</c>. Returns <c>null</c> for non-Chromium UAs
/// (iOS/Safari), which legitimately have no Client Hints.
/// </summary>
public static class UaMetadata
{
    public static object? Build(string userAgent, bool isMobile)
    {
        var ua = userAgent ?? string.Empty;
        var ver = Regex.Match(ua, @"Chrome/([\d.]+)");
        if (!ver.Success)
            return null; // Safari / non-Chromium — no UA-CH.

        var full = ver.Groups[1].Value;                 // e.g. 128.0.0.0
        var major = full.Split('.')[0];                 // e.g. 128

        string platform, platformVersion, architecture = "x86", model = "";
        if (Regex.IsMatch(ua, "Windows", RegexOptions.IgnoreCase))
        {
            platform = "Windows";
            platformVersion = "15.0.0";                 // UA-CH value for Win10/11 era
        }
        else if (Regex.IsMatch(ua, "Android", RegexOptions.IgnoreCase))
        {
            platform = "Android";
            var av = Regex.Match(ua, @"Android ([\d.]+)");
            platformVersion = (av.Success ? av.Groups[1].Value : "15") + (av.Success && !av.Groups[1].Value.Contains('.') ? ".0.0" : "");
            architecture = "";                          // Android reports empty arch in UA-CH
            var m = Regex.Match(ua, @"Android [\d.]+; ([^;)]+)[;)]");
            if (m.Success) model = m.Groups[1].Value.Trim();
        }
        else if (Regex.IsMatch(ua, "Macintosh|Mac OS X", RegexOptions.IgnoreCase))
        {
            platform = "macOS";
            platformVersion = "14.0.0";
            architecture = "arm";
        }
        else
        {
            platform = "Linux";
            platformVersion = "";
        }

        object Brand(string b, string v) => new { brand = b, version = v };

        return new
        {
            brands = new[]
            {
                Brand("Not)A;Brand", "99"),
                Brand("Google Chrome", major),
                Brand("Chromium", major),
            },
            fullVersionList = new[]
            {
                Brand("Not)A;Brand", "99.0.0.0"),
                Brand("Google Chrome", full),
                Brand("Chromium", full),
            },
            fullVersion = full,
            platform,
            platformVersion,
            architecture,
            model,
            mobile = isMobile,
            bitness = architecture == "" ? "" : "64",
            wow64 = false,
        };
    }
}
