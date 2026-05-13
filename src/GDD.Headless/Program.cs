using Microsoft.Extensions.Configuration;
using Microsoft.Extensions.DependencyInjection;
using Microsoft.Extensions.Hosting;
using Serilog;
using Serilog.Settings.Configuration;
using Serilog.Sinks.File;
using GDD.Abstractions;
using GDD.Headless.Platform;
using GDD.Mcp;
using GDD.Mcp.Tools;
using GDD.Models;
using GDD.Services;

var headed = args.Any(a => a.Equals("--headed", StringComparison.OrdinalIgnoreCase));

var browsersPath = Path.Combine(AppContext.BaseDirectory, ".browsers");
Environment.SetEnvironmentVariable("PLAYWRIGHT_BROWSERS_PATH", browsersPath);

var host = Host.CreateDefaultBuilder(args)
    .ConfigureAppConfiguration((_, config) =>
    {
        config.SetBasePath(AppContext.BaseDirectory);
        config.AddJsonFile("appsettings.json", optional: false, reloadOnChange: true);
    })
    .UseSerilog((context, loggerConfig) =>
    {
        var readerOptions = new ConfigurationReaderOptions(
            typeof(FileLoggerConfigurationExtensions).Assembly,
            typeof(ConsoleLoggerConfigurationExtensions).Assembly);
        loggerConfig.ReadFrom.Configuration(context.Configuration, readerOptions);
    })
    .ConfigureServices((context, services) =>
    {
        var config = new AppConfig();
        context.Configuration.GetSection("GDD").Bind(config);
        if (headed) config.Headed = true;
        services.AddSingleton(config);

        services.AddSingleton<IMainThreadDispatcher, ConsoleDispatcher>();

        services.AddSingleton<CdpService>();
        services.AddSingleton<DeviceEmulationService>();
        services.AddSingleton<LocationEmulationService>();
        services.AddSingleton<NetworkEmulationService>();
        services.AddSingleton<QuickAuthService>();
        services.AddSingleton<TokenInjectionService>();
        services.AddSingleton<TelegramInitDataService>();
        services.AddSingleton<TelegramInjectionService>();
        services.AddSingleton<NotificationInterceptionService>();
        services.AddSingleton<ConsoleInterceptionService>();
        services.AddSingleton<NetworkMonitoringService>();

        services.AddSingleton<HeadlessPlayerManager>();
        services.AddSingleton<IPlayerManager>(sp => sp.GetRequiredService<HeadlessPlayerManager>());

        services.AddSingleton<McpToolRegistry>();
        services.AddSingleton(sp => new McpServer(
            sp.GetRequiredService<McpToolRegistry>(),
            sp.GetRequiredService<IMainThreadDispatcher>(),
            config.McpPort,
            config.BindAddress));

        services.AddHttpClient("GDDAuth", client =>
        {
            client.BaseAddress = new Uri(config.BackendUrl.TrimEnd('/') + "/");
            client.DefaultRequestHeaders.Add("Accept", "application/json");
        });
    })
    .Build();

await PlaywrightSetup.EnsureBrowserAsync();

var registry = host.Services.GetRequiredService<McpToolRegistry>();
var playerManager = host.Services.GetRequiredService<IPlayerManager>();
var appConfig = host.Services.GetRequiredService<AppConfig>();
var dispatcher = host.Services.GetRequiredService<IMainThreadDispatcher>();
var authService = host.Services.GetRequiredService<QuickAuthService>();
var tokenService = host.Services.GetRequiredService<TokenInjectionService>();
var deviceService = host.Services.GetRequiredService<DeviceEmulationService>();
var locationService = host.Services.GetRequiredService<LocationEmulationService>();
var networkService = host.Services.GetRequiredService<NetworkEmulationService>();
var notificationService = host.Services.GetRequiredService<NotificationInterceptionService>();
var consoleService = host.Services.GetRequiredService<ConsoleInterceptionService>();
var networkMonitorService = host.Services.GetRequiredService<NetworkMonitoringService>();
var cdpService = host.Services.GetRequiredService<CdpService>();

registry.SetPlayerManager(playerManager);

PlayerTools.Register(registry, playerManager);
NavigationTools.Register(registry, playerManager);
InteractionTools.Register(registry, playerManager);
ReadTools.Register(registry, playerManager);
ExecutionTools.Register(registry, playerManager);
AuthTools.Register(registry, playerManager, authService, tokenService, dispatcher, appConfig);
EmulationTools.Register(registry, playerManager, deviceService, locationService, networkService, cdpService);
StateTools.Register(registry, playerManager, notificationService);
DiagnosticsTools.Register(registry, playerManager, consoleService, networkMonitorService, cdpService);
HelpTools.Register(registry);

var mcpServer = host.Services.GetRequiredService<McpServer>();
mcpServer.Start();

var mode = appConfig.Headed ? "headed" : "headless";
Log.Information("GDD {Mode} MCP server at http://localhost:{Port}/mcp", mode, mcpServer.ActualPort);
Console.WriteLine($"GDD ({mode}) — MCP server at http://localhost:{mcpServer.ActualPort}/mcp");
Console.WriteLine("Press Ctrl+C to stop.");

await host.WaitForShutdownAsync();

mcpServer.Dispose();
if (host.Services.GetRequiredService<HeadlessPlayerManager>() is IAsyncDisposable disposable)
    await disposable.DisposeAsync();
Log.CloseAndFlush();
