using System.IO;

namespace GDD.Models;

public sealed class AppConfig
{
    public string FrontendUrl { get; set; } = "http://localhost:5173";
    public string BackendUrl { get; set; } = "http://localhost:8080/api/v1";
    public string BotToken { get; set; } = string.Empty;
    public int McpPort { get; set; } = 9700;
    public string BindAddress { get; set; } = "localhost";
    public string DataFolderRoot { get; set; } = string.Empty;
    public bool Headed { get; set; }
    public bool CheckForUpdates { get; set; } = true;

    /// <summary>
    /// Opt-in anti-bot stealth (default off). When true, browsers launch with
    /// AutomationControlled disabled and pages get a stealth init script that masks the
    /// usual automation tells (navigator.webdriver, chrome.runtime, permissions, plugins).
    /// </summary>
    public bool Stealth { get; set; }

    /// <summary>
    /// Maximum stealth (default off, implies <see cref="Stealth"/>). Adds the headless/
    /// datacenter evasions the base script skips: coherent UA-CH metadata via CDP (fixes the
    /// worker HeadlessChrome leak + navigator.platform mismatch), spoofed WebGL vendor/renderer,
    /// realistic hardwareConcurrency/deviceMemory, faked media devices, a non-UTC default
    /// timezone, WebRTC non-proxied-UDP blocking, and removal of the --enable-automation switch.
    /// </summary>
    public bool StealthMax { get; set; }

    public string? LicenseKey { get; set; }

    public string GetDataFolderRoot()
    {
        if (!string.IsNullOrEmpty(DataFolderRoot))
            return Environment.ExpandEnvironmentVariables(DataFolderRoot);

        return Path.Combine(
            Environment.GetFolderPath(Environment.SpecialFolder.LocalApplicationData),
            "GDD",
            "Profiles");
    }
}
