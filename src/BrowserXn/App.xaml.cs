using System.Windows;
using Microsoft.Extensions.Configuration;
using Microsoft.Extensions.DependencyInjection;
using Microsoft.Extensions.Hosting;
using Serilog;
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

        _host = Host.CreateDefaultBuilder()
            .ConfigureAppConfiguration((_, config) =>
            {
                config.SetBasePath(AppContext.BaseDirectory);
                config.AddJsonFile("appsettings.json", optional: false, reloadOnChange: true);
            })
            .UseSerilog((context, loggerConfig) =>
            {
                loggerConfig.ReadFrom.Configuration(context.Configuration);
            })
            .ConfigureServices((context, services) =>
            {
                services.AddGDDServices(context.Configuration);
            })
            .Build();

        RegisterMcpTools();
        StartMcpServer();

        var mainViewModel = _host.Services.GetRequiredService<MainViewModel>();
        var mainWindow = new MainWindow { DataContext = mainViewModel };
        MainWindow = mainWindow;
        mainWindow.Show();
    }

    private void RegisterMcpTools()
    {
        var registry = _host!.Services.GetRequiredService<McpToolRegistry>();
        var mainVm = _host.Services.GetRequiredService<MainViewModel>();
        var config = _host.Services.GetRequiredService<AppConfig>();
        var authService = _host.Services.GetRequiredService<QuickAuthService>();
        var tokenService = _host.Services.GetRequiredService<TokenInjectionService>();
        var deviceService = _host.Services.GetRequiredService<DeviceEmulationService>();
        var locationService = _host.Services.GetRequiredService<LocationEmulationService>();
        var networkService = _host.Services.GetRequiredService<NetworkEmulationService>();
        var notificationService = _host.Services.GetRequiredService<NotificationInterceptionService>();
        var consoleService = _host.Services.GetRequiredService<ConsoleInterceptionService>();
        var networkMonitorService = _host.Services.GetRequiredService<NetworkMonitoringService>();
        var cdpService = _host.Services.GetRequiredService<CdpService>();

        PlayerTools.Register(registry, mainVm);
        NavigationTools.Register(registry, mainVm);
        InteractionTools.Register(registry, mainVm);
        ReadTools.Register(registry, mainVm);
        ExecutionTools.Register(registry, mainVm);
        AuthTools.Register(registry, mainVm, authService, tokenService, config);
        EmulationTools.Register(registry, mainVm, deviceService, locationService, networkService, cdpService);
        StateTools.Register(registry, mainVm, notificationService);
        DiagnosticsTools.Register(registry, mainVm, consoleService, networkMonitorService, cdpService);
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
