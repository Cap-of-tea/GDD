using System.IO;

namespace GDD.Models;

public sealed class AppConfig
{
    public string FrontendUrl { get; set; } = "http://localhost:5173";
    public string BackendUrl { get; set; } = "http://localhost:8080/api/v1";
    public string BotToken { get; set; } = string.Empty;
    public int McpPort { get; set; } = 9700;
    public string BindAddress { get; set; } = "localhost";
    public string DataFolderRoot { get; set; } = string.Empty;
    public bool Headed { get; set; }
    public bool CheckForUpdates { get; set; } = true;

    public string GetDataFolderRoot()
    {
        if (!string.IsNullOrEmpty(DataFolderRoot))
            return Environment.ExpandEnvironmentVariables(DataFolderRoot);

        return Path.Combine(
            Environment.GetFolderPath(Environment.SpecialFolder.LocalApplicationData),
            "GDD",
            "Profiles");
    }
}
