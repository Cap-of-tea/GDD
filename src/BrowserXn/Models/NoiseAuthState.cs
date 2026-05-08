using System.Text.Json.Serialization;

namespace GDD.Models;

public sealed class NoiseAuthState
{
    [JsonPropertyName("state")]
    public NoiseAuthStateInner State { get; set; } = new();

    [JsonPropertyName("version")]
    public int Version { get; set; }
}

public sealed class NoiseAuthStateInner
{
    [JsonPropertyName("accessToken")]
    public string? AccessToken { get; set; }

    [JsonPropertyName("sessionToken")]
    public string? SessionToken { get; set; }

    [JsonPropertyName("user")]
    public AuthUser? User { get; set; }

    [JsonPropertyName("platform")]
    public string? Platform { get; set; }

    [JsonPropertyName("isAuthenticated")]
    public bool IsAuthenticated { get; set; }
}
