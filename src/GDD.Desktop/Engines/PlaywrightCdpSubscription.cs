using GDD.Abstractions;

namespace GDD.Desktop.Engines;

internal sealed class PlaywrightCdpSubscription : ICdpEventSubscription
{
    private Action? _unsubscribe;

    public event EventHandler<string>? EventReceived;

    internal void Fire(string json) => EventReceived?.Invoke(this, json);

    /// <summary>Registers how to detach the underlying handler on <see cref="Dispose"/>.</summary>
    internal void OnDispose(Action unsubscribe) => _unsubscribe = unsubscribe;

    public void Dispose()
    {
        _unsubscribe?.Invoke();
        _unsubscribe = null;
    }
}
