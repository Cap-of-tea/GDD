using Avalonia.Threading;
using GDD.Abstractions;

namespace GDD.Desktop.Platform;

/// <summary>Marshals work onto the Avalonia UI thread.</summary>
public sealed class AvaloniaDispatcher : IMainThreadDispatcher
{
    public async Task InvokeAsync(Func<Task> action)
    {
        if (Dispatcher.UIThread.CheckAccess())
        {
            await action();
            return;
        }
        // Avalonia unwraps the inner Task for the Func<Task> overload, so awaiting
        // completes only when `action`'s task completes.
        await Dispatcher.UIThread.InvokeAsync(action);
    }

    public async Task InvokeAsync(Action action)
    {
        if (Dispatcher.UIThread.CheckAccess())
        {
            action();
            return;
        }
        await Dispatcher.UIThread.InvokeAsync(action);
    }
}
