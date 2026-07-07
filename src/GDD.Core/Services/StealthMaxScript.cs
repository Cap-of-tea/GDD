namespace GDD.Services;

/// <summary>
/// Extra evasions layered on top of <see cref="StealthScript"/> when
/// <see cref="GDD.Models.AppConfig.StealthMax"/> is on. Targets the headless/datacenter
/// tells the base script deliberately skips: navigator.platform coherence, SwiftShader
/// WebGL, datacenter core/memory counts, empty media-device lists, and stray Client Hints
/// on emulated Apple devices. UA/worker coherence itself is handled out-of-band via CDP
/// <c>Emulation.setUserAgentOverride</c> with full userAgentMetadata.
/// </summary>
public static class StealthMaxScript
{
    public const string Js = @"
(function () {
    var ua = navigator.userAgent || '';

    // navigator.webdriver: a real Chrome exposes it as `false`, not `undefined`.
    try { Object.defineProperty(Navigator.prototype, 'webdriver', { get: () => false, configurable: true }); } catch (e) {}
    try { Object.defineProperty(navigator, 'webdriver', { get: () => false, configurable: true }); } catch (e) {}

    // navigator.platform coherent with the UA's OS (fixes 'Linux' under a Windows UA).
    try {
        var platform = null;
        if (/Windows/i.test(ua)) platform = 'Win32';
        else if (/Macintosh|Mac OS X/i.test(ua)) platform = 'MacIntel';
        else if (/iPhone/i.test(ua)) platform = 'iPhone';
        else if (/iPad/i.test(ua)) platform = 'iPad';
        else if (/Android/i.test(ua)) platform = 'Linux armv8l';
        if (platform) Object.defineProperty(navigator, 'platform', { get: () => platform, configurable: true });
    } catch (e) {}

    // Realistic hardware — 48 datacenter cores / odd memory are a tell.
    try { Object.defineProperty(navigator, 'hardwareConcurrency', { get: () => 8, configurable: true }); } catch (e) {}
    try { Object.defineProperty(navigator, 'deviceMemory', { get: () => 8, configurable: true }); } catch (e) {}

    // WebGL vendor/renderer — SwiftShader screams headless/datacenter. Report a common iGPU.
    try {
        var spoof = { 37445: 'Intel Inc.', 37446: 'Intel(R) Iris(TM) Xe Graphics' }; // UNMASKED_VENDOR/RENDERER_WEBGL
        var patch = function (proto) {
            if (!proto || !proto.getParameter) return;
            var gp = proto.getParameter;
            proto.getParameter = function (p) { return (p in spoof) ? spoof[p] : gp.call(this, p); };
        };
        if (window.WebGLRenderingContext) patch(WebGLRenderingContext.prototype);
        if (window.WebGL2RenderingContext) patch(WebGL2RenderingContext.prototype);
    } catch (e) {}

    // Media devices: an empty list == headless. Present a plausible mic/speaker/camera set.
    try {
        if (navigator.mediaDevices && navigator.mediaDevices.enumerateDevices) {
            navigator.mediaDevices.enumerateDevices = function () {
                return Promise.resolve([
                    { deviceId: 'default', kind: 'audioinput',  label: '', groupId: 'grp-audio' },
                    { deviceId: 'default', kind: 'audiooutput', label: '', groupId: 'grp-audio' },
                    { deviceId: 'cam-1',   kind: 'videoinput',  label: '', groupId: 'grp-video' }
                ]);
            };
        }
    } catch (e) {}

    // iOS/Safari has no Client Hints — drop userAgentData when emulating Apple devices.
    try {
        if (/iPhone|iPad/i.test(ua) && 'userAgentData' in navigator) {
            Object.defineProperty(navigator, 'userAgentData', { get: () => undefined, configurable: true });
        }
    } catch (e) {}
})();
";
}
