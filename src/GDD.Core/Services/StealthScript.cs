namespace GDD.Services;

/// <summary>
/// Opt-in anti-bot init script (see <see cref="GDD.Models.AppConfig.Stealth"/>). Masks the
/// common automation tells that survive on a real headed Chromium. The launch flag
/// <c>--disable-blink-features=AutomationControlled</c> already removes navigator.webdriver;
/// this covers the rest. Kept deliberately small — GDD runs real headed Chromium, so the
/// heavy headless evasions (WebGL vendor, etc.) are unnecessary.
/// </summary>
public static class StealthScript
{
    public const string Js = @"
(function () {
    // navigator.webdriver — belt-and-suspenders (the launch flag also clears it).
    try { Object.defineProperty(Navigator.prototype, 'webdriver', { get: () => undefined }); } catch (e) {}
    try { Object.defineProperty(navigator, 'webdriver', { get: () => undefined }); } catch (e) {}

    // chrome.runtime — present in a real Chrome, absent under bare automation.
    try {
        window.chrome = window.chrome || {};
        if (!window.chrome.runtime) window.chrome.runtime = {};
    } catch (e) {}

    // permissions.query('notifications') must agree with Notification.permission.
    try {
        var origQuery = navigator.permissions && navigator.permissions.query;
        if (origQuery) {
            navigator.permissions.query = function (parameters) {
                return parameters && parameters.name === 'notifications'
                    ? Promise.resolve({ state: (typeof Notification !== 'undefined' ? Notification.permission : 'default') })
                    : origQuery.call(navigator.permissions, parameters);
            };
        }
    } catch (e) {}

    // Non-empty plugins / mimeTypes (empty arrays are a classic automation tell).
    try {
        if (navigator.plugins && navigator.plugins.length === 0) {
            Object.defineProperty(navigator, 'plugins', { get: () => [1, 2, 3, 4, 5] });
        }
    } catch (e) {}

    // languages must not be empty.
    try {
        if (!navigator.languages || navigator.languages.length === 0) {
            Object.defineProperty(navigator, 'languages', { get: () => ['en-US', 'en'] });
        }
    } catch (e) {}
})();
";
}
