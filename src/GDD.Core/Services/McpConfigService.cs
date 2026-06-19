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
            RegisterInFile(Path.Combine(home, ".claude", "mcp.json"), "gdd", entry);
            RegisterInFile(Path.Combine(home, ".cursor", "mcp.json"), "gdd", entry);
        }
        catch (Exception ex)
        {
            Logger.Warning(ex, "MCP auto-registration failed");
        }
    }

    /// <summary>
    /// Registers an HTTP MCP entry (for persistent servers like the GUI). Uses a distinct
    /// server name so it can coexist with the stdio-proxy "gdd" entry.
    /// </summary>
    public static void EnsureRegisteredHttp(string serverName, int port)
    {
        try
        {
            var entry = new JsonObject { ["url"] = $"http://localhost:{port}/mcp" };
            var home = Environment.GetFolderPath(Environment.SpecialFolder.UserProfile);
            RegisterInFile(Path.Combine(home, ".claude", "mcp.json"), serverName, entry);
            RegisterInFile(Path.Combine(home, ".cursor", "mcp.json"), serverName, entry);
        }
        catch (Exception ex)
        {
            Logger.Warning(ex, "MCP HTTP auto-registration failed");
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

    private static void RegisterInFile(string configPath, string serverName, JsonObject entry)
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

            var existing = servers[serverName];
            if (existing is not null && existing.ToJsonString() == entry.ToJsonString())
            {
                Logger.Debug("MCP server '{Name}' already registered (unchanged) in {Path}", serverName, configPath);
                return;
            }

            var updating = existing is not null;
            servers[serverName] = entry.DeepClone();

            var options = new JsonSerializerOptions { WriteIndented = true };
            File.WriteAllText(configPath, root.ToJsonString(options));
            Logger.Information("{Action} MCP server '{Name}' in {Path}",
                updating ? "Updated" : "Registered", serverName, configPath);
        }
        catch (Exception ex)
        {
            Logger.Warning(ex, "Failed to register MCP in {Path}", configPath);
        }
    }
}
