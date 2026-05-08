using System.Text.Json;
using Microsoft.Web.WebView2.Core;
using GDD.Models;
using Serilog;

namespace GDD.Services;

public sealed class TelegramInjectionService
{
    private static readonly ILogger Logger = Log.ForContext<TelegramInjectionService>();
    private readonly TelegramInitDataService _initDataService;

    public TelegramInjectionService(TelegramInitDataService initDataService)
    {
        _initDataService = initDataService;
    }

    public async Task InjectAsync(CoreWebView2 webView, TelegramUserConfig config, string botToken)
    {
        var initData = _initDataService.GenerateInitData(config, botToken);

        var initDataUnsafe = JsonSerializer.Serialize(new
        {
            user = new
            {
                id = config.TelegramUserId,
                first_name = config.FirstName,
                username = config.Username,
                language_code = config.LanguageCode
            },
            auth_date = DateTimeOffset.UtcNow.ToUnixTimeSeconds().ToString()
        });

        var script = $$"""
            (function() {
              const initData = '{{initData}}';
              const initDataUnsafe = {{initDataUnsafe}};

              const noop = function() {};
              const noopReturn = function() { return undefined; };

              const cloudStorageData = {};
              const cloudStorage = {
                setItem: function(key, value, callback) {
                  cloudStorageData[key] = value;
                  if (callback) callback(null, true);
                },
                getItem: function(key, callback) {
                  if (callback) callback(null, cloudStorageData[key] || '');
                },
                getItems: function(keys, callback) {
                  const result = {};
                  keys.forEach(k => result[k] = cloudStorageData[k] || '');
                  if (callback) callback(null, result);
                },
                removeItem: function(key, callback) {
                  delete cloudStorageData[key];
                  if (callback) callback(null, true);
                },
                removeItems: function(keys, callback) {
                  keys.forEach(k => delete cloudStorageData[k]);
                  if (callback) callback(null, true);
                },
                getKeys: function(callback) {
                  if (callback) callback(null, Object.keys(cloudStorageData));
                }
              };

              window.Telegram = {
                WebApp: {
                  initData: initData,
                  initDataUnsafe: initDataUnsafe,
                  version: '7.0',
                  platform: 'android',
                  colorScheme: 'dark',
                  themeParams: {
                    bg_color: '#1a1a2e',
                    text_color: '#e6e6e6',
                    hint_color: '#999999',
                    link_color: '#5eaef5',
                    button_color: '#5eaef5',
                    button_text_color: '#ffffff',
                    secondary_bg_color: '#16213e'
                  },
                  isExpanded: true,
                  viewportHeight: 844,
                  viewportStableHeight: 844,
                  headerColor: '#1a1a2e',
                  backgroundColor: '#1a1a2e',
                  isClosingConfirmationEnabled: false,
                  CloudStorage: cloudStorage,
                  BackButton: {
                    isVisible: false,
                    show: noop, hide: noop,
                    onClick: noop, offClick: noop
                  },
                  MainButton: {
                    text: '', color: '#5eaef5', textColor: '#ffffff',
                    isVisible: false, isActive: false, isProgressVisible: false,
                    show: noop, hide: noop, enable: noop, disable: noop,
                    showProgress: noop, hideProgress: noop,
                    setText: noop, setParams: noop,
                    onClick: noop, offClick: noop
                  },
                  HapticFeedback: {
                    impactOccurred: noop,
                    notificationOccurred: noop,
                    selectionChanged: noop
                  },
                  ready: noop,
                  expand: noop,
                  close: noop,
                  setHeaderColor: noop,
                  setBackgroundColor: noop,
                  enableClosingConfirmation: noop,
                  disableClosingConfirmation: noop,
                  onEvent: noop,
                  offEvent: noop,
                  sendData: noop,
                  openLink: function(url) { window.open(url, '_blank'); },
                  openTelegramLink: noop,
                  openInvoice: noop,
                  showPopup: noop,
                  showAlert: function(msg, cb) { alert(msg); if(cb) cb(); },
                  showConfirm: function(msg, cb) { if(cb) cb(confirm(msg)); },
                  isVersionAtLeast: function(v) { return true; }
                }
              };
            })();
            """;

        await webView.AddScriptToExecuteOnDocumentCreatedAsync(script);

        Logger.Information("Telegram WebApp injected for TG user {TgUserId} ({Username})",
            config.TelegramUserId, config.Username);
    }
}
