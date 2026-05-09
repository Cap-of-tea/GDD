using System.Text.Json.Serialization;

namespace GDD.Models;

public sealed class ApiEnvelope<T>
{
    [JsonPropertyName("success")]
    public bool Success { get; set; }

    [JsonPropertyName("data")]
    public T? Data { get; set; }

    [JsonPropertyName("meta")]
    public ApiMeta? Meta { get; set; }
}

public sealed class ApiMeta
{
    [JsonPropertyName("timestamp")]
    public string? Timestamp { get; set; }

    [JsonPropertyName("request_id")]
    public string? RequestId { get; set; }
}
