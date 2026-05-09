using System.Diagnostics;
using Microsoft.Playwright;
using Serilog;

namespace GDD.Headless.Platform;

internal static class PlaywrightSetup
{
    private static readonly ILogger Logger = Log.ForContext(typeof(PlaywrightSetup));

    public static async Task EnsureBrowserAsync()
    {
        if (await TryLaunchAsync())
        {
            Logger.Information("Chromium browser verified");
            return;
        }

        Console.WriteLine("Chromium browser not found. Installing...");
        Logger.Information("Chromium not found, auto-installing");

        if (!InstallChromium())
        {
            Console.Error.WriteLine();
            Console.Error.WriteLine("Failed to install Chromium automatically.");
            Console.Error.WriteLine("Please install manually:");
            Console.Error.WriteLine();

            if (OperatingSystem.IsWindows())
                Console.Error.WriteLine("  powershell -File playwright.ps1 install chromium");
            else
                Console.Error.WriteLine("  ./playwright.sh install chromium");

            if (OperatingSystem.IsLinux())
            {
                Console.Error.WriteLine();
                Console.Error.WriteLine("On Linux you may also need system dependencies:");
                Console.Error.WriteLine("  sudo apt install -y libnss3 libatk-bridge2.0-0 libdrm2 libxkbcommon0 libgbm1");
            }

            Environment.Exit(1);
        }

        if (!await TryLaunchAsync())
        {
            Console.Error.WriteLine("Chromium was installed but failed to launch.");

            if (OperatingSystem.IsLinux())
            {
                Console.Error.WriteLine("This usually means missing system libraries.");
                Console.Error.WriteLine("Install them with:");
                Console.Error.WriteLine("  sudo apt install -y libnss3 libatk-bridge2.0-0 libdrm2 libxkbcommon0 libgbm1 libpango-1.0-0 libcairo2 libasound2");
            }
            else if (OperatingSystem.IsMacOS())
            {
                Console.Error.WriteLine("On macOS, ensure the app is not blocked by Gatekeeper.");
            }

            Environment.Exit(1);
        }

        Console.WriteLine("Chromium installed successfully.");
        Logger.Information("Chromium installed and verified");
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
        try
        {
            var exitCode = Microsoft.Playwright.Program.Main(["install", "chromium"]);
            return exitCode == 0;
        }
        catch (Exception ex)
        {
            Logger.Warning(ex, "Playwright.Program.Main failed, trying process fallback");
            return InstallViaProcess();
        }
    }

    private static bool InstallViaProcess()
    {
        try
        {
            var baseDir = AppContext.BaseDirectory;
            ProcessStartInfo psi;

            if (OperatingSystem.IsWindows())
            {
                var script = Path.Combine(baseDir, "playwright.ps1");
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
                    FileName = "/bin/bash",
                    Arguments = $"-c \"dotnet exec '{Path.Combine(baseDir, "Microsoft.Playwright.dll")}' install chromium\"",
                    UseShellExecute = false
                };
            }

            var process = Process.Start(psi);
            process?.WaitForExit(300_000);
            return process?.ExitCode == 0;
        }
        catch (Exception ex)
        {
            Logger.Error(ex, "Chromium installation via process failed");
            return false;
        }
    }
}
