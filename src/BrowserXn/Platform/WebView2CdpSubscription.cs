using Microsoft.Web.WebView2.Core;
using GDD.Abstractions;

namespace GDD.Platform;

public sealed class WebView2CdpSubscription : ICdpEventSubscription
{
    private readonly CoreWebView2DevToolsProtocolEventReceiver _receiver;

    public event EventHandler<string>? EventReceived;

    public WebView2CdpSubscription(CoreWebView2 webView, string eventName)
    {
        _receiver = webView.GetDevToolsProtocolEventReceiver(eventName);
        _receiver.DevToolsProtocolEventReceived += OnEvent;
    }

    private void OnEvent(object? sender, CoreWebView2DevToolsProtocolEventReceivedEventArgs e)
    {
        EventReceived?.Invoke(this, e.ParameterObjectAsJson);
    }

    public void Dispose()
    {
        _receiver.DevToolsProtocolEventReceived -= OnEvent;
    }
}
