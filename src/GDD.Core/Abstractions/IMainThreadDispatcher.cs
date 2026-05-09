namespace GDD.Abstractions;

public interface IMainThreadDispatcher
{
    Task InvokeAsync(Func<Task> action);
    Task InvokeAsync(Action action);
}
