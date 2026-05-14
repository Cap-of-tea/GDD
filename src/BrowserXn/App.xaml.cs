using System.Windows;
using Microsoft.Extensions.Configuration;
using Microsoft.Extensions.DependencyInjection;
using Microsoft.Extensions.Hosting;
using Serilog;
using Serilog.Settings.Configuration;
using Serilog.Sinks.File;
using GDD.Abstractions;
using GDD.Interop;
using GDD.Mcp;
using GDD.Mcp.Tools;
using GDD.Models;
using GDD.Services;
using GDD.Views;
using GDD.ViewModels;

namespace GDD;

public partial class App : Application
{
    private IHost? _host;
    private McpServer? _mcpServer;

    protected override void OnStartup(StartupEventArgs e)
    {
        base.OnStartup(e);

        if (!WebView2Check.EnsureRuntime())
        {
            Shutdown();
            return;
        }

        _host = Host.CreateDefaultBuilder()
            .ConfigureAppConfiguration((_, config) =>
            {
                config.SetBasePath(AppContext.BaseDirectory);
                config.AddJsonFile("appsettings.json", optional: false, reloadOnChange: true);
            })
            .UseSerilog((context, loggerConfig) =>
            {
                var readerOptions = new ConfigurationReaderOptions(
                    typeof(FileLoggerConfigurationExtensions).Assembly);
                loggerConfig.ReadFrom.Configuration(context.Configuration, readerOptions);
            })
            .ConfigureServices((context, services) =>
            {
                services.AddGDDServices(context.Configuration);
            })
            .Build();

        RegisterMcpTools();
        StartMcpServer();

        if (_mcpServer is not null)
        {
            var updateService = _host.Services.GetRequiredService<UpdateService>();
            _mcpServer.SetUpdateService(updateService);
        }

        var mainViewModel = _host.Services.GetRequiredService<MainViewModel>();
        if (_mcpServer is not null)
            mainViewModel.ApiEndpoint = $"http://localhost:{_mcpServer.ActualPort}/mcp";
        var mainWindow = new MainWindow { DataContext = mainViewModel };
        MainWindow = mainWindow;
        mainWindow.Show();

        var appConfig = _host.Services.GetRequiredService<AppConfig>();
        if (appConfig.CheckForUpdates)
        {
            _ = Task.Run(async () =>
            {
                try
                {
                    var updateService = _host.Services.GetRequiredService<UpdateService>();
                    var update = await updateService.CheckForUpdateAsync();
                    if (update is not null)
                        Dispatcher.Invoke(() => mainViewModel.SetUpdateAvailable(update));
                }
                catch { /* non-critical */ }
            });
        }
    }

    private void RegisterMcpTools()
    {
        var registry = _host!.Services.GetRequiredService<McpToolRegistry>();
        var playerManager = _host.Services.GetRequiredService<IPlayerManager>();
        var config = _host.Services.GetRequiredService<AppConfig>();
        var dispatcher = _host.Services.GetRequiredService<IMainThreadDispatcher>();
        var authService = _host.Services.GetRequiredService<QuickAuthService>();
        var tokenService = _host.Services.GetRequiredService<TokenInjectionService>();
        var deviceService = _host.Services.GetRequiredService<DeviceEmulationService>();
        var locationService = _host.Services.GetRequiredService<LocationEmulationService>();
        var networkService = _host.Services.GetRequiredService<NetworkEmulationService>();
        var notificationService = _host.Services.GetRequiredService<NotificationInterceptionService>();
        var consoleService = _host.Services.GetRequiredService<ConsoleInterceptionService>();
        var networkMonitorService = _host.Services.GetRequiredService<NetworkMonitoringService>();
        var cdpService = _host.Services.GetRequiredService<CdpService>();

        registry.SetPlayerManager(playerManager);

        PlayerTools.Register(registry, playerManager);
        NavigationTools.Register(registry, playerManager);
        InteractionTools.Register(registry, playerManager);
        ReadTools.Register(registry, playerManager);
        ExecutionTools.Register(registry, playerManager);
        AuthTools.Register(registry, playerManager, authService, tokenService, dispatcher, config);
        EmulationTools.Register(registry, playerManager, deviceService, locationService, networkService, cdpService);
        StateTools.Register(registry, playerManager, notificationService);
        DiagnosticsTools.Register(registry, playerManager, consoleService, networkMonitorService, cdpService);
        HelpTools.Register(registry);

        var updateService = _host.Services.GetRequiredService<UpdateService>();
        UpdateTools.Register(registry, updateService);
    }

    private void StartMcpServer()
    {
        try
        {
            _mcpServer = _host!.Services.GetRequiredService<McpServer>();
            _mcpServer.Start();
            Log.Information("MCP server available at http://localhost:{Port}", _mcpServer.ActualPort);
        }
        catch (Exception ex)
        {
            Log.Error(ex, "Failed to start MCP server");
        }
    }

    protected override void OnExit(ExitEventArgs e)
    {
        _mcpServer?.Dispose();
        _host?.Dispose();
        Log.CloseAndFlush();
        base.OnExit(e);
    }
}
