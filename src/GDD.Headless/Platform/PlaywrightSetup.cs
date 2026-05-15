using System.Diagnostics;
using System.Runtime.InteropServices;
using Microsoft.Playwright;
using Serilog;

namespace GDD.Headless.Platform;

internal static class PlaywrightSetup
{
    private static readonly ILogger Logger = Log.ForContext(typeof(PlaywrightSetup));

    public static async Task EnsureBrowserAsync()
    {
        if (!OperatingSystem.IsWindows())
        {
            FixUnixPermissions();
            if (OperatingSystem.IsMacOS())
                RemoveQuarantine();
        }

        if (await TryLaunchAsync())
        {
            Logger.Information("Chromium browser verified");
            return;
        }

        Console.WriteLine("Chromium browser not found. Installing...");
        Logger.Information("Chromium not found, auto-installing");

        if (!InstallChromium())
        {
            var msg = "Failed to install Chromium automatically.";
            Console.Error.WriteLine();
            Console.Error.WriteLine(msg);
            Logger.Error(msg);
            PrintManualInstallInstructions();
            Environment.Exit(1);
        }

        if (!OperatingSystem.IsWindows())
        {
            FixUnixPermissions();
            if (OperatingSystem.IsMacOS())
                RemoveQuarantine();
        }

        if (!await TryLaunchAsync())
        {
            var msg = "Chromium was installed but failed to launch.";
            Console.Error.WriteLine(msg);
            Logger.Error(msg);

            if (OperatingSystem.IsLinux())
            {
                Console.Error.WriteLine("This usually means missing system libraries.");
                Console.Error.WriteLine("Install them with:");
                Console.Error.WriteLine("  sudo apt install -y libnss3 libatk-bridge2.0-0 libdrm2 libxkbcommon0 libgbm1 libpango-1.0-0 libcairo2 libasound2");
            }
            else if (OperatingSystem.IsMacOS())
            {
                Console.Error.WriteLine("Try removing Gatekeeper quarantine:");
                Console.Error.WriteLine("  xattr -dr com.apple.quarantine . 2>/dev/null || true");
            }

            Environment.Exit(1);
        }

        Console.WriteLine("Chromium installed successfully.");
        Logger.Information("Chromium installed and verified");
    }

    private static void PrintManualInstallInstructions()
    {
        Console.Error.WriteLine("Please install manually:");
        Console.Error.WriteLine();

        var browsersPath = Environment.GetEnvironmentVariable("PLAYWRIGHT_BROWSERS_PATH") ?? ".browsers";

        if (OperatingSystem.IsWindows())
        {
            Console.Error.WriteLine($"  $env:PLAYWRIGHT_BROWSERS_PATH=\"{browsersPath}\"");
            Console.Error.WriteLine("  powershell -File playwright.ps1 install chromium");
        }
        else
        {
            var nodePath = GetBundledNodePath();
            var cliPath = Path.Combine(AppContext.BaseDirectory, ".playwright", "package", "cli.js");

            Console.Error.WriteLine($"  PLAYWRIGHT_BROWSERS_PATH=\"{browsersPath}\" \\");
            Console.Error.WriteLine($"    {nodePath} \\");
            Console.Error.WriteLine($"    {cliPath} install chromium");

            if (OperatingSystem.IsMacOS())
            {
                Console.Error.WriteLine();
                Console.Error.WriteLine("  Then remove Gatekeeper quarantine:");
                Console.Error.WriteLine("  xattr -dr com.apple.quarantine .browsers .playwright 2>/dev/null || true");
            }
            else
            {
                Console.Error.WriteLine();
                Console.Error.WriteLine("  You may also need system dependencies:");
                Console.Error.WriteLine("  sudo apt install -y libnss3 libatk-bridge2.0-0 libdrm2 libxkbcommon0 libgbm1");
            }
        }
    }

    private static void RemoveQuarantine()
    {
        try
        {
            var baseDir = AppContext.BaseDirectory;
            var targets = new[] { ".browsers", ".playwright", "GDD.Headless" };
            foreach (var target in targets)
            {
                var path = Path.Combine(baseDir, target);
                if (!File.Exists(path) && !Directory.Exists(path)) continue;

                var psi = new ProcessStartInfo("xattr", ["-dr", "com.apple.quarantine", path])
                {
                    UseShellExecute = false,
                    RedirectStandardOutput = true,
                    RedirectStandardError = true
                };
                var proc = Process.Start(psi);
                proc?.WaitForExit(10_000);
            }
            Logger.Debug("Removed quarantine attributes");
        }
        catch (Exception ex)
        {
            Logger.Debug("Failed to remove quarantine: {Message}", ex.Message);
        }
    }

    private static void FixUnixPermissions()
    {
        try
        {
            var baseDir = AppContext.BaseDirectory;
            foreach (var dir in new[] { ".browsers", ".playwright" })
            {
                var path = Path.Combine(baseDir, dir);
                if (!Directory.Exists(path)) continue;

                Logger.Debug("Fixing permissions on {Path}", path);
                var psi = new ProcessStartInfo("chmod", ["-R", "+x", path])
                {
                    UseShellExecute = false,
                    RedirectStandardOutput = true,
                    RedirectStandardError = true
                };
                var proc = Process.Start(psi);
                proc?.WaitForExit(10_000);
            }
        }
        catch (Exception ex)
        {
            Logger.Debug("Failed to fix permissions: {Message}", ex.Message);
        }
    }

    private static async Task<bool> TryLaunchAsync()
    {
        try
        {
            using var pw = await Playwright.CreateAsync();
            var browser = await pw.Chromium.LaunchAsync(new BrowserTypeLaunchOptions { Headless = true });
            var version = browser.Version;
            await browser.CloseAsync();
            Logger.Debug("Chromium version: {Version}", version);
            return true;
        }
        catch (Exception ex)
        {
            Logger.Debug("Chromium launch check failed: {Message}", ex.Message);
            return false;
        }
    }

    private static bool InstallChromium()
    {
        if (!OperatingSystem.IsWindows())
            FixUnixPermissions();

        try
        {
            Environment.SetEnvironmentVariable("PLAYWRIGHT_DRIVER_SEARCH_PATH", AppContext.BaseDirectory);
            var exitCode = Microsoft.Playwright.Program.Main(["install", "chromium"]);
            if (exitCode == 0) return true;
        }
        catch (Exception ex)
        {
            Logger.Warning(ex, "Playwright.Program.Main failed, trying fallback");
        }

        if (InstallViaProcess())
            return true;

        if (!OperatingSystem.IsWindows())
            return InstallViaBundledNode();

        return false;
    }

    private static bool InstallViaProcess()
    {
        try
        {
            var script = Path.Combine(AppContext.BaseDirectory, "playwright.ps1");
            ProcessStartInfo psi;

            if (OperatingSystem.IsWindows())
            {
                psi = new ProcessStartInfo
                {
                    FileName = "powershell",
                    Arguments = $"-NoProfile -ExecutionPolicy Bypass -File \"{script}\" install chromium",
                    UseShellExecute = false
                };
            }
            else
            {
                psi = new ProcessStartInfo
                {
                    FileName = "pwsh",
                    Arguments = $"-NoProfile -File \"{script}\" install chromium",
                    UseShellExecute = false
                };
            }

            var process = Process.Start(psi);
            process?.WaitForExit(300_000);
            return process?.ExitCode == 0;
        }
        catch (Exception ex)
        {
            Logger.Warning("pwsh not available: {Message}", ex.Message);
            return false;
        }
    }

    private static bool InstallViaBundledNode()
    {
        try
        {
            var nodePath = GetBundledNodePath();
            var cliPath = Path.Combine(AppContext.BaseDirectory, ".playwright", "package", "cli.js");

            if (!File.Exists(nodePath) || !File.Exists(cliPath))
            {
                Logger.Debug("Bundled node or cli.js not found: node={Node}, cli={Cli}", nodePath, cliPath);
                return false;
            }

            Logger.Information("Installing Chromium via bundled Node.js + Playwright CLI");
            Console.WriteLine("Installing via bundled Playwright CLI (pwsh not available)...");

            var psi = new ProcessStartInfo(nodePath, [cliPath, "install", "chromium"])
            {
                UseShellExecute = false,
                WorkingDirectory = AppContext.BaseDirectory
            };
            psi.Environment["PLAYWRIGHT_BROWSERS_PATH"] =
                Environment.GetEnvironmentVariable("PLAYWRIGHT_BROWSERS_PATH") ?? Path.Combine(AppContext.BaseDirectory, ".browsers");

            var process = Process.Start(psi);
            process?.WaitForExit(600_000);
            return process?.ExitCode == 0;
        }
        catch (Exception ex)
        {
            Logger.Error(ex, "Chromium installation via bundled node failed");
            return false;
        }
    }

    private static string GetBundledNodePath()
    {
        var platform = OperatingSystem.IsMacOS()
            ? $"darwin-{(RuntimeInformation.OSArchitecture == Architecture.Arm64 ? "arm64" : "x64")}"
            : "linux-x64";
        return Path.Combine(AppContext.BaseDirectory, ".playwright", "node", platform, "node");
    }
}
