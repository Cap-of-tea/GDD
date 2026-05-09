using System.Diagnostics;
using System.IO;
using System.Net.Http;
using System.Windows;
using Microsoft.Web.WebView2.Core;

namespace GDD.Interop;

internal static class WebView2Check
{
    private const string BootstrapperUrl =
        "https://go.microsoft.com/fwlink/p/?LinkId=2124703";

    public static bool EnsureRuntime()
    {
        try
        {
            var version = CoreWebView2Environment.GetAvailableBrowserVersionString();
            if (!string.IsNullOrEmpty(version))
                return true;
        }
        catch
        {
            // not installed
        }

        var result = MessageBox.Show(
            "GDD requires Microsoft Edge WebView2 Runtime to work.\n\n" +
            "It is free and provided by Microsoft.\n\n" +
            "Install now?",
            "WebView2 Runtime Not Found",
            MessageBoxButton.YesNo,
            MessageBoxImage.Warning);

        if (result != MessageBoxResult.Yes)
        {
            MessageBox.Show(
                "GDD cannot run without WebView2 Runtime.\n\n" +
                "You can install it later from:\n" +
                "https://developer.microsoft.com/en-us/microsoft-edge/webview2/",
                "GDD",
                MessageBoxButton.OK,
                MessageBoxImage.Information);
            return false;
        }

        return DownloadAndInstall();
    }

    private static bool DownloadAndInstall()
    {
        var tempPath = Path.Combine(Path.GetTempPath(), "MicrosoftEdgeWebview2Setup.exe");

        try
        {
            using var client = new HttpClient();
            var data = client.GetByteArrayAsync(BootstrapperUrl).GetAwaiter().GetResult();
            File.WriteAllBytes(tempPath, data);
        }
        catch (Exception ex)
        {
            MessageBox.Show(
                $"Failed to download WebView2 installer:\n{ex.Message}\n\n" +
                "Please install manually from:\n" +
                "https://developer.microsoft.com/en-us/microsoft-edge/webview2/",
                "Download Error",
                MessageBoxButton.OK,
                MessageBoxImage.Error);
            return false;
        }

        try
        {
            var process = Process.Start(new ProcessStartInfo
            {
                FileName = tempPath,
                Arguments = "/install",
                UseShellExecute = true
            });

            process?.WaitForExit();

            try
            {
                var version = CoreWebView2Environment.GetAvailableBrowserVersionString();
                if (!string.IsNullOrEmpty(version))
                {
                    MessageBox.Show(
                        $"WebView2 Runtime {version} installed successfully!\n\nGDD will now start.",
                        "Installation Complete",
                        MessageBoxButton.OK,
                        MessageBoxImage.Information);
                    return true;
                }
            }
            catch
            {
                // still not available
            }

            MessageBox.Show(
                "WebView2 installation may not have completed.\n\n" +
                "Please restart GDD after installation finishes.",
                "GDD",
                MessageBoxButton.OK,
                MessageBoxImage.Warning);
            return false;
        }
        catch (Exception ex)
        {
            MessageBox.Show(
                $"Failed to run WebView2 installer:\n{ex.Message}\n\n" +
                "Please install manually from:\n" +
                "https://developer.microsoft.com/en-us/microsoft-edge/webview2/",
                "Installation Error",
                MessageBoxButton.OK,
                MessageBoxImage.Error);
            return false;
        }
        finally
        {
            try { File.Delete(tempPath); } catch { }
        }
    }
}
