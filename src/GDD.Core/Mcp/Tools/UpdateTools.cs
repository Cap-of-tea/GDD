using System.Text.Json;
using GDD.Services;

namespace GDD.Mcp.Tools;

public static class UpdateTools
{
    public static void Register(McpToolRegistry registry, UpdateService updateService)
    {
        registry.Register(
            new McpToolDefinition
            {
                Name = "gdd_check_update",
                Description = "Check if a newer version of GDD is available.",
                InputSchema = new { type = "object", properties = new { } }
            },
            async _ =>
            {
                var update = await updateService.CheckForUpdateAsync();
                if (update is null)
                    return McpResult.Text($"You are running the latest version (v{GddVersion.Current}).");

                var result = new
                {
                    current_version = GddVersion.Current,
                    latest_version = update.Version,
                    update_available = true,
                    download_url = update.DownloadUrl,
                    size_mb = Math.Round(update.SizeBytes / 1048576.0, 1),
                    release_notes = update.ReleaseNotes.Length > 500
                        ? update.ReleaseNotes[..500] + "..."
                        : update.ReleaseNotes
                };
                return McpResult.Text(JsonSerializer.Serialize(result, new JsonSerializerOptions { WriteIndented = true }));
            });

        registry.Register(
            new McpToolDefinition
            {
                Name = "gdd_update",
                Description = "Download and install a GDD update. Requires confirm=true. GDD will restart after update.",
                InputSchema = new
                {
                    type = "object",
                    properties = new
                    {
                        confirm = new
                        {
                            type = "boolean",
                            description = "Must be true to proceed. This will restart GDD."
                        }
                    },
                    required = new[] { "confirm" }
                }
            },
            async args =>
            {
                var confirm = args?.TryGetProperty("confirm", out var cEl) == true && cEl.GetBoolean();
                if (!confirm)
                    return McpResult.Error("Safety check: set confirm=true to proceed. GDD will download the update, apply it, and restart. Local settings (appsettings.json) will be preserved.");

                var update = await updateService.CheckForUpdateAsync();
                if (update is null)
                    return McpResult.Text($"Already up to date (v{GddVersion.Current}).");

                var archive = await updateService.DownloadUpdateAsync(update);
                await updateService.ApplyUpdateAsync(archive);

                return McpResult.Text($"Update to v{update.Version} applied. GDD is restarting...");
            });
    }
}
