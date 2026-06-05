using Avalonia.Threading;
using GDD.Abstractions;

namespace GDD.Desktop.Platform;

/// <summary>Marshals work onto the Avalonia UI thread.</summary>
public sealed class AvaloniaDispatcher : IMainThreadDispatcher
{
    public Task InvokeAsync(Func<Task> action)
    {
        if (Dispatcher.UIThread.CheckAccess())
            return action();
        return Dispatcher.UIThread.InvokeAsync(action);
    }

    public Task InvokeAsync(Action action)
    {
        if (Dispatcher.UIThread.CheckAccess())
        {
            action();
            return Task.CompletedTask;
        }
        return Dispatcher.UIThread.InvokeAsync(action).GetTask();
    }
}
