using GDD.Abstractions;

namespace GDD.Headless.Engines;

internal sealed class PlaywrightCdpSubscription : ICdpEventSubscription
{
    public event EventHandler<string>? EventReceived;

    internal void Fire(string json) => EventReceived?.Invoke(this, json);

    public void Dispose() { }
}
