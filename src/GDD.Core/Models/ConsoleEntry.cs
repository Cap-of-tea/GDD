namespace GDD.Models;

public sealed class ConsoleEntry
{
    public int PlayerId { get; init; }
    public string Level { get; init; } = "log";
    public string Message { get; init; } = "";
    public string? Source { get; init; }
    public int? LineNumber { get; init; }
    public int? ColumnNumber { get; init; }
    public string? StackTrace { get; init; }
    public bool IsException { get; init; }
    public DateTimeOffset Timestamp { get; init; } = DateTimeOffset.Now;
}
