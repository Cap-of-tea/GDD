using System.Windows;
using System.Windows.Threading;
using GDD.Abstractions;

namespace GDD.Platform;

public sealed class WpfDispatcher : IMainThreadDispatcher
{
    public async Task InvokeAsync(Func<Task> action)
    {
        var tcs = new TaskCompletionSource();
        await Application.Current.Dispatcher.InvokeAsync(async () =>
        {
            try
            {
                await action();
                tcs.SetResult();
            }
            catch (Exception ex)
            {
                tcs.SetException(ex);
            }
        }, DispatcherPriority.Normal);
        await tcs.Task;
    }

    public async Task InvokeAsync(Action action)
    {
        await Application.Current.Dispatcher.InvokeAsync(action, DispatcherPriority.Normal);
    }
}
