namespace GDD.Models;

public sealed class TelegramUserConfig
{
    public long TelegramUserId { get; set; }
    public string Username { get; set; } = string.Empty;
    public string FirstName { get; set; } = string.Empty;
    public string LanguageCode { get; set; } = "en";
}
