using System.Security.Cryptography;
using System.Text;
using System.Text.Json;
using Serilog;

namespace GDD.Services;

public sealed class LicenseService
{
    private static readonly ILogger Logger = Log.ForContext<LicenseService>();

    private const string PublicKeyBase64 =
        "MFkwEwYHKoZIzj0CAQYIKoZIzj0DAQcDQgAEhOt1AiiD471daR4Bt4RWEYN0di/OS8iYyf7sSczkrLkXteHLSXS0h4epYXz/g2nt/ZZTsz/6zy0JZuAFKgFZBA==";

    public bool IsLicensed { get; private set; }
    public string? LicensedTo { get; private set; }
    public DateTimeOffset? ExpiresAt { get; private set; }

    public LicenseService(string? licenseKey)
    {
        Validate(licenseKey);
    }

    private void Validate(string? licenseKey)
    {
        if (string.IsNullOrWhiteSpace(licenseKey))
            return;

        try
        {
            var parts = licenseKey.Trim().Split('.');
            if (parts.Length != 2)
            {
                Logger.Warning("License key has invalid format (expected payload.signature)");
                return;
            }

            var payloadBytes = Convert.FromBase64String(parts[0]);
            var signatureBytes = Convert.FromBase64String(parts[1]);

            using var ecdsa = ECDsa.Create();
            ecdsa.ImportSubjectPublicKeyInfo(Convert.FromBase64String(PublicKeyBase64), out _);

            if (!ecdsa.VerifyData(payloadBytes, signatureBytes, HashAlgorithmName.SHA256))
            {
                Logger.Warning("License key signature verification failed");
                return;
            }

            var payload = JsonSerializer.Deserialize<LicensePayload>(
                Encoding.UTF8.GetString(payloadBytes));

            if (payload is null)
            {
                Logger.Warning("License key payload deserialization returned null");
                return;
            }

            if (payload.Exp is { } exp)
            {
                ExpiresAt = exp;
                if (exp < DateTimeOffset.UtcNow)
                {
                    Logger.Warning("License key expired on {Date}", exp);
                    return;
                }
            }

            IsLicensed = true;
            LicensedTo = payload.Sub;
            Logger.Information("Valid license: {Licensee}, expires {Exp}",
                payload.Sub ?? "(unnamed)", payload.Exp?.ToString("yyyy-MM-dd") ?? "never");
        }
        catch (FormatException)
        {
            Logger.Warning("License key contains invalid base64");
        }
        catch (JsonException)
        {
            Logger.Warning("License key payload is not valid JSON");
        }
        catch (Exception ex)
        {
            Logger.Warning(ex, "License key validation failed");
        }
    }

    private sealed class LicensePayload
    {
        [System.Text.Json.Serialization.JsonPropertyName("sub")]
        public string? Sub { get; set; }

        [System.Text.Json.Serialization.JsonPropertyName("email")]
        public string? Email { get; set; }

        [System.Text.Json.Serialization.JsonPropertyName("iat")]
        public DateTimeOffset? Iat { get; set; }

        [System.Text.Json.Serialization.JsonPropertyName("exp")]
        public DateTimeOffset? Exp { get; set; }
    }
}
