using Avalonia.Controls;
using Avalonia.Interactivity;
using GDD.Desktop.Platform;
using GDD.Models;

namespace GDD.Desktop.Views;

public partial class CellSettingsWindow : Window
{
    public DevicePreset SelectedDevice => (DevicePreset)DeviceCombo.SelectedItem!;
    public LocationPreset? SelectedLocation =>
        LocationCombo.SelectedIndex <= 0 ? null : (LocationPreset)LocationCombo.SelectedItem!;
    public NetworkPreset SelectedNetwork => (NetworkPreset)NetworkCombo.SelectedItem!;

    public bool TelegramEnabled => TelegramEnabledCheck.IsChecked == true;
    public TelegramUserConfig? TelegramConfig { get; private set; }
    public string? NavigateUrl { get; private set; }

    // Parameterless ctor for the XAML loader / designer.
    public CellSettingsWindow()
    {
        InitializeComponent();
    }

    public CellSettingsWindow(DesktopPlayerContext player, string defaultUrl = "") : this()
    {
        TitleText.Text = $"{player.PlayerName} — Settings";
        UrlBox.Text = string.IsNullOrEmpty(player.CurrentUrl) ? defaultUrl : player.CurrentUrl;

        DeviceCombo.ItemsSource = DevicePresets.All;
        DeviceCombo.SelectedItem = player.SelectedDevice;

        var locations = new List<object> { new NoneLocation() };
        locations.AddRange(LocationPresets.All.Cast<object>());
        LocationCombo.ItemsSource = locations;
        LocationCombo.SelectedItem = player.SelectedLocation ?? locations[0];

        NetworkCombo.ItemsSource = NetworkPresets.All;
        NetworkCombo.SelectedItem = player.SelectedNetwork;

        TelegramEnabledCheck.IsChecked = player.TelegramEnabled;
        TgUserIdBox.Text = player.TelegramUserId > 0 ? player.TelegramUserId.ToString() : "";
        TgUsernameBox.Text = player.TelegramUsername;
        TgFirstNameBox.Text = player.TelegramFirstName;
        SetLanguage(player.TelegramLanguageCode);
    }

    private void SetLanguage(string lang)
    {
        for (var i = 0; i < TgLanguageCombo.ItemCount; i++)
        {
            if (TgLanguageCombo.Items[i] is ComboBoxItem item && (string?)item.Content == lang)
            {
                TgLanguageCombo.SelectedIndex = i;
                return;
            }
        }
        TgLanguageCombo.SelectedIndex = 0;
    }

    private void OnApply(object? sender, RoutedEventArgs e)
    {
        NavigateUrl = UrlBox.Text?.Trim();

        if (TelegramEnabled)
        {
            long.TryParse(TgUserIdBox.Text, out var tgId);
            TelegramConfig = new TelegramUserConfig
            {
                TelegramUserId = tgId,
                Username = TgUsernameBox.Text ?? "",
                FirstName = TgFirstNameBox.Text ?? "",
                LanguageCode = (TgLanguageCombo.SelectedItem as ComboBoxItem)?.Content as string ?? "en"
            };
        }

        Close(true);
    }

    private void OnCancel(object? sender, RoutedEventArgs e) => Close(false);
}

internal sealed class NoneLocation
{
    public string CityName => "(None)";
}
