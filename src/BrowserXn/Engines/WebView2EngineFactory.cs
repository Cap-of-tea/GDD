using System.IO;
using GDD.Abstractions;
using GDD.Models;

namespace GDD.Engines;

public sealed class WebView2EngineFactory : IBrowserEngineFactory
{
    private readonly AppConfig _config;

    public WebView2EngineFactory(AppConfig config)
    {
        _config = config;
    }

    public IBrowserEngine Create(int playerId)
    {
        var userDataFolder = Path.Combine(
            _config.GetDataFolderRoot(),
            $"Player_{playerId}");

        return new WebView2Engine(playerId, userDataFolder);
    }
}
