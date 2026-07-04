using System.Security.Cryptography;
using System.Text;
using System.Text.Json;

if (args.Length == 0 || args.Contains("--help"))
{
    Console.WriteLine("GDD License Key Generator");
    Console.WriteLine();
    Console.WriteLine("Usage: LicenseKeyGen --key <private-key-base64> --sub <licensee> --email <email> [--months <N>]");
    Console.WriteLine();
    Console.WriteLine("Options:");
    Console.WriteLine("  --key      ECDSA P-256 private key (base64, PKCS#8 DER)");
    Console.WriteLine("  --sub      Licensee name (company or person)");
    Console.WriteLine("  --email    Licensee contact email");
    Console.WriteLine("  --months   License duration in months (default: 12, 0 = perpetual)");
    return;
}

string? privateKeyB64 = null, sub = null, email = null;
int months = 12;

for (int i = 0; i < args.Length - 1; i++)
{
    switch (args[i])
    {
        case "--key": privateKeyB64 = args[++i]; break;
        case "--sub": sub = args[++i]; break;
        case "--email": email = args[++i]; break;
        case "--months": months = int.Parse(args[++i]); break;
    }
}

if (privateKeyB64 is null) { Console.Error.WriteLine("Error: --key is required"); return; }
if (sub is null) { Console.Error.WriteLine("Error: --sub is required"); return; }
if (email is null) { Console.Error.WriteLine("Error: --email is required"); return; }

using var ecdsa = ECDsa.Create();
ecdsa.ImportECPrivateKey(Convert.FromBase64String(privateKeyB64), out _);

var payload = new
{
    sub,
    email,
    iat = DateTimeOffset.UtcNow,
    exp = months > 0 ? (DateTimeOffset?)DateTimeOffset.UtcNow.AddMonths(months) : null
};

var payloadJson = JsonSerializer.Serialize(payload, new JsonSerializerOptions { WriteIndented = false });
var payloadBytes = Encoding.UTF8.GetBytes(payloadJson);
var signature = ecdsa.SignData(payloadBytes, HashAlgorithmName.SHA256);

var licenseKey = $"{Convert.ToBase64String(payloadBytes)}.{Convert.ToBase64String(signature)}";

Console.WriteLine($"Licensee:    {sub}");
Console.WriteLine($"Email:       {email}");
Console.WriteLine($"Issued:      {DateTimeOffset.UtcNow:yyyy-MM-dd}");
Console.WriteLine($"Expires:     {(months > 0 ? DateTimeOffset.UtcNow.AddMonths(months).ToString("yyyy-MM-dd") : "never")}");
Console.WriteLine();
Console.WriteLine("License Key:");
Console.WriteLine(licenseKey);
Console.WriteLine();
Console.WriteLine("Add to appsettings.json:");
Console.WriteLine($"  \"LicenseKey\": \"{licenseKey}\"");
