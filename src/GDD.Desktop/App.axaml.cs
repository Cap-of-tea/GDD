using Avalonia;
using Avalonia.Controls.ApplicationLifetimes;
using Avalonia.Markup.Xaml;
using GDD;
using GDD.Abstractions;
using GDD.Desktop.Platform;
using GDD.Desktop.Services;
using GDD.Desktop.ViewModels;
using GDD.Desktop.Views;
using GDD.Mcp;
using GDD.Mcp.Tools;
using GDD.Models;
using GDD.Services;
using Microsoft.Extensions.Configuration;
using Microsoft.Extensions.DependencyInjection;
using Serilog;
using Serilog.Settings.Configuration;

namespace GDD.Desktop;

public partial class App : Application
{
    private ServiceProvider? _services;
    private McpServer? _mcpServer;
    private DesktopPlayerManager? _manager;

    public override void Initialize() => AvaloniaXamlLoader.Load(this);

    public override void OnFrameworkInitializationCompleted()
    {
        if (ApplicationLifetime is IClassicDesktopStyleApplicationLifetime desktop)
        {
            var provider = BuildServices();
            _services = provider;
            _manager = provider.GetRequiredService<DesktopPlayerManager>();

            StartMcp(provider);

            // Verify/install Chromium in the background; browser launches lazily on first AddPlayers.
            _ = Task.Run(PlaywrightSetup.EnsureBrowserAsync);

            var vm = provider.GetRequiredService<MainViewModel>();
            if (_mcpServer is not null)
                vm.ApiEndpoint = $"http://localhost:{_mcpServer.ActualPort}/mcp";
            desktop.MainWindow = new MainWindow { DataContext = vm };

            desktop.ShutdownRequested += (_, _) =>
            {
                try { _mcpServer?.Dispose(); } catch { }
                try { _manager?.DisposeAsync().AsTask().GetAwaiter().GetResult(); } catch { }
                Log.CloseAndFlush();
            };
        }

        base.OnFrameworkInitializationCompleted();
    }

    private static ServiceProvider BuildServices()
    {
        var configuration = new ConfigurationBuilder()
            .SetBasePath(AppContext.BaseDirectory)
            .AddJsonFile("appsettings.json", optional: false, reloadOnChange: true)
            .Build();

        var readerOptions = new ConfigurationReaderOptions(
            typeof(FileLoggerConfigurationExtensions).Assembly,
            typeof(ConsoleLoggerConfigurationExtensions).Assembly);
        Log.Logger = new LoggerConfiguration()
            .ReadFrom.Configuration(configuration, readerOptions)
            .CreateLogger();

        var config = new AppConfig();
        configuration.GetSection("GDD").Bind(config);
        if (Program.HeadedOverride is { } headed) config.Headed = headed;

        var services = new ServiceCollection();
        services.AddSingleton(config);

        services.AddSingleton<IMainThreadDispatcher, AvaloniaDispatcher>();
        services.AddSingleton<IThumbnailService, ScreencastThumbnailService>();

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

        services.AddSingleton<DesktopPlayerManager>();
        services.AddSingleton<IPlayerManager>(sp => sp.GetRequiredService<DesktopPlayerManager>());

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
        services.AddHttpClient("GitHubApi", client =>
        {
            client.DefaultRequestHeaders.Add("User-Agent", "GDD-Updater");
            client.DefaultRequestHeaders.Add("Accept", "application/vnd.github+json");
        });
        services.AddSingleton<UpdateService>();

        services.AddSingleton<MainViewModel>();

        return services.BuildServiceProvider();
    }

    private void StartMcp(IServiceProvider provider)
    {
        var registry = provider.GetRequiredService<McpToolRegistry>();
        var playerManager = provider.GetRequiredService<IPlayerManager>();
        var appConfig = provider.GetRequiredService<AppConfig>();
        var dispatcher = provider.GetRequiredService<IMainThreadDispatcher>();
        var authService = provider.GetRequiredService<QuickAuthService>();
        var tokenService = provider.GetRequiredService<TokenInjectionService>();
        var deviceService = provider.GetRequiredService<DeviceEmulationService>();
        var locationService = provider.GetRequiredService<LocationEmulationService>();
        var networkService = provider.GetRequiredService<NetworkEmulationService>();
        var notificationService = provider.GetRequiredService<NotificationInterceptionService>();
        var consoleService = provider.GetRequiredService<ConsoleInterceptionService>();
        var networkMonitorService = provider.GetRequiredService<NetworkMonitoringService>();
        var cdpService = provider.GetRequiredService<CdpService>();

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

        var updateService = provider.GetRequiredService<UpdateService>();
        UpdateTools.Register(registry, updateService);
        registry.SetUpdateService(updateService);

        _mcpServer = provider.GetRequiredService<McpServer>();
        _mcpServer.SetUpdateService(updateService);
        _mcpServer.Start();

        var mode = appConfig.Headed ? "headed" : "headless";
        Log.Information("GDD.Desktop v{Version} {Mode} MCP server at http://localhost:{Port}/mcp",
            GddVersion.Current, mode, _mcpServer.ActualPort);
        Console.WriteLine($"GDD.Desktop v{GddVersion.Current} ({mode}) — MCP server at http://localhost:{_mcpServer.ActualPort}/mcp");
    }
}
