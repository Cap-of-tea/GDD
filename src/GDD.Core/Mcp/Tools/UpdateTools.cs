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
                Description = "Check if a newer version of GDD is available on GitHub Releases. Returns current version, latest version, download URL, file size, and release notes. No side effects — only checks, does not download or install.",
                InputSchema = new { type = "object", properties = new { } },
                Annotations = new { readOnlyHint = true, destructiveHint = false, idempotentHint = true, openWorldHint = true }
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
                Description = "Download and install a GDD update from GitHub Releases. Requires confirm=true as a safety check. Downloads the archive, extracts it over the current installation, and restarts GDD. Local settings (appsettings.json) are preserved. The MCP connection will briefly drop during restart.",
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
                },
                Annotations = new { readOnlyHint = false, destructiveHint = true, idempotentHint = false, openWorldHint = true }
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

                _ = Task.Run(async () =>
                {
                    await Task.Delay(2000);
                    await updateService.ApplyUpdateAsync(archive);
                });

                return McpResult.Text($"Update to v{update.Version} downloaded. GDD is restarting — the MCP connection will drop briefly.");
            });
    }
}
