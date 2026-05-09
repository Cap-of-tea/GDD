using System.Text.Json;
using GDD.Abstractions;
using Serilog;

namespace GDD.Services;

public sealed class CdpService
{
    private static readonly ILogger Logger = Log.ForContext<CdpService>();

    public async Task CallAsync(IBrowserEngine engine, string method, object parameters)
    {
        var json = JsonSerializer.Serialize(parameters, new JsonSerializerOptions
        {
            PropertyNamingPolicy = JsonNamingPolicy.CamelCase
        });

        Logger.Debug("CDP {Method}: {Params}", method, json);
        await engine.CallCdpMethodAsync(method, json);
    }

    public async Task<string> CallWithResultAsync(IBrowserEngine engine, string method, object parameters)
    {
        var json = JsonSerializer.Serialize(parameters, new JsonSerializerOptions
        {
            PropertyNamingPolicy = JsonNamingPolicy.CamelCase
        });

        Logger.Debug("CDP {Method}: {Params}", method, json);
        return await engine.CallCdpMethodWithResultAsync(method, json);
    }
}
