namespace GDD.Models;

public sealed class NetworkEntry
{
    public int PlayerId { get; init; }
    public string RequestId { get; init; } = "";
    public string Method { get; init; } = "";
    public string Url { get; init; } = "";
    public string? ResourceType { get; init; }
    public int StatusCode { get; set; }
    public string? StatusText { get; set; }
    public string? MimeType { get; set; }
    public long? ContentLength { get; set; }
    public DateTimeOffset RequestTime { get; init; } = DateTimeOffset.Now;
    public DateTimeOffset? ResponseTime { get; set; }
    public double? DurationMs { get; set; }
    public string? ErrorText { get; set; }
    public bool Failed { get; set; }
    public bool Completed { get; set; }
}
