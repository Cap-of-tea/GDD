namespace GDD.Abstractions;

public interface ICdpEventSubscription : IDisposable
{
    event EventHandler<string> EventReceived;
}
