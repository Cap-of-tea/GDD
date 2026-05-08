using System.Security.Cryptography;
using System.Text;
using System.Text.Json;
using GDD.Models;

namespace GDD.Services;

public sealed class TelegramInitDataService
{
    public string GenerateInitData(TelegramUserConfig config, string botToken)
    {
        var authDate = DateTimeOffset.UtcNow.ToUnixTimeSeconds();

        var userJson = JsonSerializer.Serialize(new
        {
            id = config.TelegramUserId,
            first_name = config.FirstName,
            username = config.Username,
            language_code = config.LanguageCode
        });

        var parameters = new SortedDictionary<string, string>
        {
            ["auth_date"] = authDate.ToString(),
            ["user"] = userJson
        };

        var dataCheckString = string.Join("\n",
            parameters.Select(kv => $"{kv.Key}={kv.Value}"));

        using var hmacSecret = new HMACSHA256(Encoding.UTF8.GetBytes("WebAppData"));
        var secretKey = hmacSecret.ComputeHash(Encoding.UTF8.GetBytes(botToken));

        using var hmacHash = new HMACSHA256(secretKey);
        var hashBytes = hmacHash.ComputeHash(Encoding.UTF8.GetBytes(dataCheckString));
        var hash = Convert.ToHexString(hashBytes).ToLowerInvariant();

        parameters["hash"] = hash;

        return string.Join("&",
            parameters.Select(kv =>
                $"{Uri.EscapeDataString(kv.Key)}={Uri.EscapeDataString(kv.Value)}"));
    }
}
