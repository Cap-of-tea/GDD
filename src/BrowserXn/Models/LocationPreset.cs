namespace GDD.Models;

public sealed record LocationPreset(
    string CityName,
    double Latitude,
    double Longitude,
    double Accuracy,
    string TimezoneId,
    string Locale);

public static class LocationPresets
{
    public static readonly LocationPreset Moscow = new("Moscow", 55.7558, 37.6173, 10, "Europe/Moscow", "ru-RU");
    public static readonly LocationPreset SaintPetersburg = new("Saint Petersburg", 59.9343, 30.3351, 10, "Europe/Moscow", "ru-RU");
    public static readonly LocationPreset NewYork = new("New York", 40.7128, -74.0060, 10, "America/New_York", "en-US");
    public static readonly LocationPreset London = new("London", 51.5074, -0.1278, 10, "Europe/London", "en-GB");
    public static readonly LocationPreset Tokyo = new("Tokyo", 35.6762, 139.6503, 10, "Asia/Tokyo", "ja-JP");

    public static IReadOnlyList<LocationPreset> All { get; } = new[]
    {
        Moscow, SaintPetersburg, NewYork, London, Tokyo
    };
}
