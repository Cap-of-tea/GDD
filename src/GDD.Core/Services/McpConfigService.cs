using System.Text.Json;
using System.Text.Json.Nodes;
using Serilog;

namespace GDD.Services;

public static class McpConfigService
{
    private static readonly ILogger Logger = Log.ForContext(typeof(McpConfigService));

    public static void EnsureRegistered(string appBaseDir)
    {
        try
        {
            var scriptsDir = Path.Combine(appBaseDir, "Scripts");
            string proxyPath;

            if (OperatingSystem.IsWindows())
            {
                proxyPath = Path.Combine(scriptsDir, "mcp-proxy.ps1");
            }
            else
            {
                proxyPath = Path.Combine(scriptsDir, "mcp-proxy.sh");
            }

            if (!File.Exists(proxyPath))
            {
                Logger.Debug("MCP proxy script not found at {Path}, skipping auto-registration", proxyPath);
                return;
            }

            var entry = BuildEntry(proxyPath);

            var home = Environment.GetFolderPath(Environment.SpecialFolder.UserProfile);
            RegisterInFile(Path.Combine(home, ".claude", "mcp.json"), entry);
            RegisterInFile(Path.Combine(home, ".cursor", "mcp.json"), entry);
        }
        catch (Exception ex)
        {
            Logger.Warning(ex, "MCP auto-registration failed");
        }
    }

    private static JsonObject BuildEntry(string proxyPath)
    {
        if (OperatingSystem.IsWindows())
        {
            return new JsonObject
            {
                ["command"] = "powershell",
                ["args"] = new JsonArray(
                    "-NoProfile",
                    "-ExecutionPolicy",
                    "Bypass",
                    "-File",
                    proxyPath.Replace('\\', '/'))
            };
        }

        return new JsonObject
        {
            ["command"] = "bash",
            ["args"] = new JsonArray(proxyPath)
        };
    }

    private static void RegisterInFile(string configPath, JsonObject entry)
    {
        try
        {
            var dir = Path.GetDirectoryName(configPath);
            if (dir is null) return;

            JsonObject root;

            if (File.Exists(configPath))
            {
                var text = File.ReadAllText(configPath);
                var parsed = JsonNode.Parse(text);
                if (parsed is not JsonObject obj)
                {
                    Logger.Warning("MCP config {Path} is not a JSON object, skipping", configPath);
                    return;
                }
                root = obj;
            }
            else
            {
                Directory.CreateDirectory(dir);
                root = new JsonObject();
            }

            if (root["mcpServers"] is not JsonObject servers)
            {
                servers = new JsonObject();
                root["mcpServers"] = servers;
            }

            if (servers["gdd"] is not null)
            {
                Logger.Debug("MCP server 'gdd' already registered in {Path}", configPath);
                return;
            }

            servers["gdd"] = entry.DeepClone();

            var options = new JsonSerializerOptions { WriteIndented = true };
            File.WriteAllText(configPath, root.ToJsonString(options));
            Logger.Information("Registered MCP server 'gdd' in {Path}", configPath);
        }
        catch (Exception ex)
        {
            Logger.Warning(ex, "Failed to register MCP in {Path}", configPath);
        }
    }
}
