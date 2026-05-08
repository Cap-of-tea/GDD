using System.Windows;
using System.Windows.Controls;
using System.Windows.Input;
using GDD.Models;
using GDD.ViewModels;

namespace GDD.Views;

public partial class CellSettingsWindow : Window
{
    private readonly BrowserCellViewModel _vm;

    public DevicePreset SelectedDevice => (DevicePreset)DeviceCombo.SelectedItem;
    public LocationPreset? SelectedLocation => LocationCombo.SelectedIndex == 0 ? null : (LocationPreset)LocationCombo.SelectedItem;
    public NetworkPreset SelectedNetwork => (NetworkPreset)NetworkCombo.SelectedItem;

    public bool TelegramEnabled => TelegramEnabledCheck.IsChecked == true;
    public TelegramUserConfig? TelegramConfig { get; private set; }
    public string? NavigateUrl { get; private set; }

    public CellSettingsWindow(BrowserCellViewModel vm, string defaultUrl = "")
    {
        InitializeComponent();
        _vm = vm;

        TitleRun.Text = $"{vm.PlayerName} — Settings";

        UrlBox.Text = vm.CurrentUrl;
        if (!string.IsNullOrEmpty(defaultUrl))
            UrlBox.Tag = defaultUrl;

        DeviceCombo.ItemsSource = DevicePresets.All;
        DeviceCombo.SelectedItem = vm.SelectedDevice;

        var locations = new object[] { new NoneLocation() }.Concat(LocationPresets.All.Cast<object>()).ToArray();
        LocationCombo.ItemsSource = locations;
        LocationCombo.SelectedItem = vm.SelectedLocation ?? locations[0];

        NetworkCombo.ItemsSource = NetworkPresets.All;
        NetworkCombo.SelectedItem = vm.SelectedNetwork;

        TelegramEnabledCheck.IsChecked = vm.TelegramEnabled;
        TgUserIdBox.Text = vm.TelegramUserId > 0 ? vm.TelegramUserId.ToString() : "";
        TgUsernameBox.Text = vm.TelegramUsername;
        TgFirstNameBox.Text = vm.TelegramFirstName;
        SetLanguageCombo(vm.TelegramLanguageCode);

        TelegramFields.Visibility = vm.TelegramEnabled ? Visibility.Visible : Visibility.Collapsed;
        TelegramEnabledCheck.Checked += (_, _) => TelegramFields.Visibility = Visibility.Visible;
        TelegramEnabledCheck.Unchecked += (_, _) => TelegramFields.Visibility = Visibility.Collapsed;
    }

    private void SetLanguageCombo(string lang)
    {
        foreach (ComboBoxItem item in TgLanguageCombo.Items)
        {
            if (item.Content?.ToString() == lang)
            {
                TgLanguageCombo.SelectedItem = item;
                return;
            }
        }
        TgLanguageCombo.SelectedIndex = 0;
    }

    private void NavigateToUrl()
    {
        var url = UrlBox.Text.Trim();
        if (string.IsNullOrEmpty(url)) return;

        if (!url.Contains("://"))
            url = "https://" + url;

        _vm.WebView?.CoreWebView2?.Navigate(url);
        _vm.CurrentUrl = url;
    }

    private void OnGo(object sender, RoutedEventArgs e)
    {
        NavigateToUrl();
    }

    private void OnUrlKeyDown(object sender, KeyEventArgs e)
    {
        if (e.Key == Key.Enter)
            NavigateToUrl();
    }

    private void OnApply(object sender, RoutedEventArgs e)
    {
        NavigateUrl = UrlBox.Text.Trim();

        if (TelegramEnabled)
        {
            long.TryParse(TgUserIdBox.Text, out var tgId);
            TelegramConfig = new TelegramUserConfig
            {
                TelegramUserId = tgId,
                Username = TgUsernameBox.Text,
                FirstName = TgFirstNameBox.Text,
                LanguageCode = (TgLanguageCombo.SelectedItem as ComboBoxItem)?.Content?.ToString() ?? "en"
            };
        }

        DialogResult = true;
    }

    private void OnCancel(object sender, RoutedEventArgs e)
    {
        DialogResult = false;
    }
}

internal class NoneLocation
{
    public string CityName => "(None)";
}
