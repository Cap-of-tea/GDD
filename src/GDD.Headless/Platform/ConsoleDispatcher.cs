using GDD.Abstractions;

namespace GDD.Headless.Platform;

public sealed class ConsoleDispatcher : IMainThreadDispatcher
{
    public async Task InvokeAsync(Func<Task> action) => await action();

    public Task InvokeAsync(Action action)
    {
        action();
        return Task.CompletedTask;
    }
}
