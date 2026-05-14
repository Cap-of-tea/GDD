using System.Diagnostics;
using System.Formats.Tar;
using System.IO.Compression;
using System.Net.Http;
using System.Runtime.InteropServices;
using System.Text.Json;
using Serilog;

namespace GDD.Services;

public sealed class UpdateService
{
    private static readonly ILogger Logger = Log.ForContext<UpdateService>();
    private static readonly string[] PreserveFiles = ["appsettings.json"];
    private static readonly string[] PreserveDirs = ["logs"];

    private readonly IHttpClientFactory _httpClientFactory;
    private readonly bool _isGui;
    private UpdateInfo? _cachedUpdate;
    private bool _checked;

    public UpdateInfo? CachedUpdate => _cachedUpdate;

    public record UpdateInfo(string Version, string DownloadUrl, string ReleaseNotes, long SizeBytes);

    public UpdateService(IHttpClientFactory httpClientFactory, bool isGui = false)
    {
        _httpClientFactory = httpClientFactory;
        _isGui = isGui;
    }

    public async Task<UpdateInfo?> CheckForUpdateAsync(CancellationToken ct = default)
    {
        if (_checked) return _cachedUpdate;

        try
        {
            var client = _httpClientFactory.CreateClient("GitHubApi");
            var response = await client.GetAsync(
                "https://api.github.com/repos/Cap-of-tea/GDD/releases/latest", ct);

            if (!response.IsSuccessStatusCode)
            {
                Logger.Warning("GitHub API returned {StatusCode}", response.StatusCode);
                _checked = true;
                return null;
            }

            var json = await response.Content.ReadAsStringAsync(ct);
            using var doc = JsonDocument.Parse(json);
            var root = doc.RootElement;

            var tagName = root.GetProperty("tag_name").GetString() ?? "";
            var latestVersion = tagName.TrimStart('v');

            if (!Version.TryParse(latestVersion, out var latest) ||
                !Version.TryParse(GddVersion.Current, out var current) ||
                latest <= current)
            {
                Logger.Information("No update available (current={Current}, latest={Latest})",
                    GddVersion.Current, latestVersion);
                _checked = true;
                return null;
            }

            var rid = GetRuntimeId();
            var assetName = $"gdd-{rid}-update.tar.gz";
            var fallbackName = $"gdd-{rid}.tar.gz";
            string? downloadUrl = null;
            long sizeBytes = 0;

            if (root.TryGetProperty("assets", out var assets))
            {
                foreach (var asset in assets.EnumerateArray())
                {
                    var name = asset.GetProperty("name").GetString() ?? "";
                    if (name == assetName)
                    {
                        downloadUrl = asset.GetProperty("browser_download_url").GetString();
                        sizeBytes = asset.GetProperty("size").GetInt64();
                        break;
                    }
                    if (name == fallbackName && downloadUrl is null)
                    {
                        downloadUrl = asset.GetProperty("browser_download_url").GetString();
                        sizeBytes = asset.GetProperty("size").GetInt64();
                    }
                }
            }

            if (downloadUrl is null)
            {
                Logger.Warning("No matching asset found for RID {Rid} in release {Version}", rid, latestVersion);
                _checked = true;
                return null;
            }

            var releaseNotes = root.TryGetProperty("body", out var bodyEl)
                ? bodyEl.GetString() ?? ""
                : "";

            _cachedUpdate = new UpdateInfo(latestVersion, downloadUrl, releaseNotes, sizeBytes);
            _checked = true;

            Logger.Information("Update available: v{Current} → v{Latest} ({SizeMb:F1} MB)",
                GddVersion.Current, latestVersion, sizeBytes / 1048576.0);

            return _cachedUpdate;
        }
        catch (Exception ex)
        {
            Logger.Warning(ex, "Failed to check for updates");
            _checked = true;
            return null;
        }
    }

    public async Task<string> DownloadUpdateAsync(UpdateInfo info, IProgress<double>? progress = null, CancellationToken ct = default)
    {
        var client = _httpClientFactory.CreateClient("GitHubApi");
        var tempPath = Path.Combine(Path.GetTempPath(), $"gdd-update-{info.Version}.tar.gz");

        using var response = await client.GetAsync(info.DownloadUrl, HttpCompletionOption.ResponseHeadersRead, ct);
        response.EnsureSuccessStatusCode();

        var totalBytes = response.Content.Headers.ContentLength ?? info.SizeBytes;
        var buffer = new byte[81920];
        long bytesRead = 0;

        await using var contentStream = await response.Content.ReadAsStreamAsync(ct);
        await using var fileStream = new FileStream(tempPath, FileMode.Create, FileAccess.Write, FileShare.None, 81920, true);

        int read;
        while ((read = await contentStream.ReadAsync(buffer, ct)) > 0)
        {
            await fileStream.WriteAsync(buffer.AsMemory(0, read), ct);
            bytesRead += read;
            if (totalBytes > 0)
                progress?.Report((double)bytesRead / totalBytes);
        }

        Logger.Information("Downloaded update to {Path} ({SizeMb:F1} MB)", tempPath, bytesRead / 1048576.0);
        return tempPath;
    }

    public async Task ApplyUpdateAsync(string archivePath)
    {
        var appDir = AppContext.BaseDirectory.TrimEnd(Path.DirectorySeparatorChar);
        var parentDir = Path.GetDirectoryName(appDir) ?? appDir;
        var stagingDir = Path.Combine(parentDir, ".gdd-update-staging");

        if (Directory.Exists(stagingDir))
            Directory.Delete(stagingDir, true);
        Directory.CreateDirectory(stagingDir);

        await using var fileStream = File.OpenRead(archivePath);
        await using var gzipStream = new GZipStream(fileStream, CompressionMode.Decompress);
        await TarFile.ExtractToDirectoryAsync(gzipStream, stagingDir, overwriteFiles: true);

        RemovePreservedFromStaging(stagingDir);

        Logger.Information("Extracted update to staging: {StagingDir}", stagingDir);

        var pid = Environment.ProcessId;
        var exePath = Environment.ProcessPath ?? "";

        if (OperatingSystem.IsWindows())
        {
            var scriptPath = Path.Combine(stagingDir, "apply-update.ps1");
            if (!File.Exists(scriptPath))
                await WriteWindowsScript(scriptPath);

            Process.Start(new ProcessStartInfo
            {
                FileName = "powershell.exe",
                Arguments = $"-ExecutionPolicy Bypass -WindowStyle Hidden -File \"{scriptPath}\" -ProcessId {pid} -StagingDir \"{stagingDir}\" -TargetDir \"{appDir}\" -ExePath \"{exePath}\"",
                UseShellExecute = false,
                CreateNoWindow = true
            });
        }
        else
        {
            var scriptPath = Path.Combine(stagingDir, "apply-update.sh");
            if (!File.Exists(scriptPath))
                await WriteUnixScript(scriptPath);

            Process.Start(new ProcessStartInfo
            {
                FileName = "/bin/bash",
                Arguments = $"\"{scriptPath}\" {pid} \"{stagingDir}\" \"{appDir}\" \"{exePath}\"",
                UseShellExecute = false
            });
        }

        Logger.Information("Update script launched, GDD will restart shortly");

        try { File.Delete(archivePath); }
        catch { /* best effort */ }

        await Task.Delay(500);
        Environment.Exit(0);
    }

    private void RemovePreservedFromStaging(string stagingDir)
    {
        foreach (var file in PreserveFiles)
        {
            var path = Path.Combine(stagingDir, file);
            if (File.Exists(path))
                File.Delete(path);
        }
        foreach (var dir in PreserveDirs)
        {
            var path = Path.Combine(stagingDir, dir);
            if (Directory.Exists(path))
                Directory.Delete(path, true);
        }
    }

    private string GetRuntimeId()
    {
        if (OperatingSystem.IsWindows())
            return _isGui ? "windows-gui" : "win-x64";
        if (OperatingSystem.IsLinux())
            return "linux-x64";
        if (OperatingSystem.IsMacOS())
            return RuntimeInformation.OSArchitecture == Architecture.Arm64 ? "macos-arm64" : "macos-x64";
        return "unknown";
    }

    private static async Task WriteWindowsScript(string path)
    {
        await File.WriteAllTextAsync(path, """
            param(
                [int]$ProcessId,
                [string]$StagingDir,
                [string]$TargetDir,
                [string]$ExePath
            )

            $maxWait = 30
            $waited = 0
            while ($waited -lt $maxWait) {
                try {
                    $proc = Get-Process -Id $ProcessId -ErrorAction Stop
                    Start-Sleep -Seconds 1
                    $waited++
                } catch {
                    break
                }
            }

            Start-Sleep -Seconds 1

            Get-ChildItem -Path $StagingDir -Recurse -File | ForEach-Object {
                $relativePath = $_.FullName.Substring($StagingDir.Length + 1)
                $destPath = Join-Path $TargetDir $relativePath
                $destDir = Split-Path $destPath -Parent
                if (-not (Test-Path $destDir)) {
                    New-Item -ItemType Directory -Path $destDir -Force | Out-Null
                }
                Copy-Item -Path $_.FullName -Destination $destPath -Force
            }

            Remove-Item -Path $StagingDir -Recurse -Force -ErrorAction SilentlyContinue

            if ($ExePath -and (Test-Path $ExePath)) {
                Start-Process -FilePath $ExePath
            }
            """);
    }

    private static async Task WriteUnixScript(string path)
    {
        await File.WriteAllTextAsync(path, """
            #!/bin/bash
            PID=$1
            STAGING=$2
            TARGET=$3
            EXE=$4

            WAITED=0
            while [ $WAITED -lt 30 ] && kill -0 $PID 2>/dev/null; do
                sleep 1
                WAITED=$((WAITED + 1))
            done

            sleep 1

            cp -rf "$STAGING"/* "$TARGET"/
            rm -rf "$STAGING"

            if [ -n "$EXE" ] && [ -f "$EXE" ]; then
                chmod +x "$EXE"
                nohup "$EXE" > /dev/null 2>&1 &
            fi
            """);

        // Make script executable
        Process.Start("chmod", $"+x \"{path}\"")?.WaitForExit();
    }
}
