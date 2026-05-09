using Microsoft.Extensions.Configuration;
using Microsoft.Extensions.DependencyInjection;
using GDD.Abstractions;
using GDD.Mcp;
using GDD.Models;
using GDD.Platform;
using GDD.ViewModels;

namespace GDD.Services;

public static class IServiceCollectionExtensions
{
    public static IServiceCollection AddGDDServices(
        this IServiceCollection services,
        IConfiguration configuration)
    {
        var config = new AppConfig();
        configuration.GetSection("GDD").Bind(config);
        services.AddSingleton(config);

        services.AddSingleton<IMainThreadDispatcher, WpfDispatcher>();

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

        services.AddSingleton<MainViewModel>(sp => new MainViewModel(
            sp.GetRequiredService<AppConfig>(),
            sp.GetRequiredService<IMainThreadDispatcher>(),
            sp.GetRequiredService<DeviceEmulationService>(),
            sp.GetRequiredService<LocationEmulationService>(),
            sp.GetRequiredService<NetworkEmulationService>(),
            sp.GetRequiredService<TelegramInjectionService>(),
            sp.GetRequiredService<QuickAuthService>(),
            sp.GetRequiredService<TokenInjectionService>(),
            sp.GetRequiredService<NotificationInterceptionService>(),
            sp.GetRequiredService<ConsoleInterceptionService>(),
            sp.GetRequiredService<NetworkMonitoringService>()));
        services.AddSingleton<IPlayerManager>(sp => sp.GetRequiredService<MainViewModel>());

        services.AddSingleton<McpToolRegistry>();
        services.AddSingleton(sp => new McpServer(
            sp.GetRequiredService<McpToolRegistry>(),
            sp.GetRequiredService<IMainThreadDispatcher>(),
            config.McpPort));

        services.AddHttpClient("GDDAuth", client =>
        {
            client.BaseAddress = new Uri(config.BackendUrl.TrimEnd('/') + "/");
            client.DefaultRequestHeaders.Add("Accept", "application/json");
        });

        return services;
    }
}
