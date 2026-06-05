using Avalonia;

namespace GDD.Desktop;

internal static class Program
{
    /// <summary>Headed override from CLI: null = use config, true/false = force.</summary>
    public static bool? HeadedOverride { get; private set; }

    [STAThread]
    public static int Main(string[] args)
    {
        if (args.Any(a => a is "--help" or "-h" or "-?" or "/?" or "--version" or "-v"))
        {
            Console.WriteLine($"GDD.Desktop v{GddVersion.Current} — Multi-Browser Testing GUI + MCP Server");
            Console.WriteLine();
            Console.WriteLine("Usage: GDD.Desktop [options]");
            Console.WriteLine("  --headed     Visible Chromium windows (default)");
            Console.WriteLine("  --headless   No Chromium UI (thumbnails still work)");
            Console.WriteLine("  --help       Show this help");
            return 0;
        }

        if (args.Any(a => a.Equals("--headed", StringComparison.OrdinalIgnoreCase))) HeadedOverride = true;
        if (args.Any(a => a.Equals("--headless", StringComparison.OrdinalIgnoreCase))) HeadedOverride = false;

        var browsersPath = Path.Combine(AppContext.BaseDirectory, ".browsers");
        Environment.SetEnvironmentVariable("PLAYWRIGHT_BROWSERS_PATH", browsersPath);

        // Diagnostics: capture Avalonia logs (incl. binding errors, which go to Trace) to a file.
        if (Environment.GetEnvironmentVariable("GDD_TRACE") is { Length: > 0 })
        {
            var traceFile = Path.Combine(AppContext.BaseDirectory, "logs", "avalonia-trace.log");
            Directory.CreateDirectory(Path.GetDirectoryName(traceFile)!);
            System.Diagnostics.Trace.Listeners.Add(new System.Diagnostics.TextWriterTraceListener(traceFile));
            System.Diagnostics.Trace.AutoFlush = true;
        }

        try
        {
            BuildAvaloniaApp().StartWithClassicDesktopLifetime(args);
            return 0;
        }
        catch (Exception ex)
        {
            var msg = $"[{DateTime.Now:yyyy-MM-dd HH:mm:ss}] FATAL: {ex}";
            Console.Error.WriteLine(msg);
            try
            {
                var logDir = Path.Combine(AppContext.BaseDirectory, "logs");
                Directory.CreateDirectory(logDir);
                File.AppendAllText(Path.Combine(logDir, "gdd-crash.log"), msg + Environment.NewLine);
            }
            catch { /* best effort */ }
            return 1;
        }
    }

    public static AppBuilder BuildAvaloniaApp()
        => AppBuilder.Configure<App>()
            .UsePlatformDetect()
            .WithInterFont()
            .LogToTrace();
}
