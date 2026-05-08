namespace GDD.Models;

public sealed class PushNotification
{
    public int PlayerId { get; init; }
    public string Title { get; init; } = string.Empty;
    public string Body { get; init; } = string.Empty;
    public string? IconUri { get; init; }
    public string? BadgeUri { get; init; }
    public string? Tag { get; init; }
    public DateTimeOffset ReceivedAt { get; init; } = DateTimeOffset.Now;
}
