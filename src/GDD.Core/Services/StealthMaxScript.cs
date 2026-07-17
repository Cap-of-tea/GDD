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

    // --- toString spoofing: make every function/getter we patch report as [native code],
    // so a detector calling Function.prototype.toString on it can't tell it was replaced.
    // (This also hardens the navigator patches below, which were toString-detectable before.)
    var _origToString = Function.prototype.toString;
    var _native = new WeakMap(); // patched fn -> the native-looking string it should report
    function markNative(fn, label) {
        try { _native.set(fn, 'function ' + (label || fn.name || '') + '() { [native code] }'); } catch (e) {}
        return fn;
    }
    var _toString = function toString() {
        var s = _native.get(this);
        return s !== undefined ? s : _origToString.call(this);
    };
    markNative(_toString, 'toString');
    try { Function.prototype.toString = _toString; } catch (e) {}

    // Define a getter that reports as native when introspected.
    function defineNativeGetter(obj, prop, value) {
        try {
            var g = markNative(function () { return value; }, 'get ' + prop);
            Object.defineProperty(obj, prop, { get: g, configurable: true });
        } catch (e) {}
    }
    // Replace a method with a native-looking implementation.
    function spoofMethod(obj, name, impl) {
        try { obj[name] = markNative(impl, name); } catch (e) {}
    }

    // navigator.webdriver: a real Chrome exposes it as `false`, not `undefined`.
    defineNativeGetter(Navigator.prototype, 'webdriver', false);
    defineNativeGetter(navigator, 'webdriver', false);

    // navigator.platform coherent with the UA's OS (fixes 'Linux' under a Windows UA).
    try {
        var platform = null;
        if (/Windows/i.test(ua)) platform = 'Win32';
        else if (/Macintosh|Mac OS X/i.test(ua)) platform = 'MacIntel';
        else if (/iPhone/i.test(ua)) platform = 'iPhone';
        else if (/iPad/i.test(ua)) platform = 'iPad';
        else if (/Android/i.test(ua)) platform = 'Linux armv8l';
        if (platform) defineNativeGetter(navigator, 'platform', platform);
    } catch (e) {}

    // Realistic hardware — 48 datacenter cores / odd memory are a tell.
    defineNativeGetter(navigator, 'hardwareConcurrency', 8);
    defineNativeGetter(navigator, 'deviceMemory', 8);

    // AltGr fidelity: CDP can't set getModifierState('AltGraph'), so a real AltGr keystroke
    // (@, €, é on DE/FR) would report false. GDD emits a genuine AltGraph key around AltGr
    // characters; we track it here and answer getModifierState('AltGraph') accordingly. The
    // shim reports as native via the toString spoof above, so it isn't itself a tell.
    try {
        var _altGraph = false;
        window.addEventListener('keydown', function (e) { if (e.code === 'AltRight' || e.key === 'AltGraph') _altGraph = true; }, true);
        window.addEventListener('keyup', function (e) { if (e.code === 'AltRight' || e.key === 'AltGraph') _altGraph = false; }, true);
        var _origGMS = KeyboardEvent.prototype.getModifierState;
        spoofMethod(KeyboardEvent.prototype, 'getModifierState', function (k) {
            if (k === 'AltGraph' && _altGraph) return true;
            return _origGMS.call(this, k);
        });
    } catch (e) {}

    // WebGL vendor/renderer — SwiftShader screams headless/datacenter. Report a common iGPU.
    try {
        var spoof = { 37445: 'Intel Inc.', 37446: 'Intel(R) Iris(TM) Xe Graphics' }; // UNMASKED_VENDOR/RENDERER_WEBGL
        var patch = function (proto) {
            if (!proto || !proto.getParameter) return;
            var gp = proto.getParameter;
            spoofMethod(proto, 'getParameter', function (p) { return (p in spoof) ? spoof[p] : gp.call(this, p); });
        };
        if (window.WebGLRenderingContext) patch(WebGLRenderingContext.prototype);
        if (window.WebGL2RenderingContext) patch(WebGL2RenderingContext.prototype);
    } catch (e) {}

    // Media devices: an empty list == headless. Present a plausible mic/speaker/camera set.
    try {
        if (navigator.mediaDevices && navigator.mediaDevices.enumerateDevices) {
            spoofMethod(navigator.mediaDevices, 'enumerateDevices', function () {
                return Promise.resolve([
                    { deviceId: 'default', kind: 'audioinput',  label: '', groupId: 'grp-audio' },
                    { deviceId: 'default', kind: 'audiooutput', label: '', groupId: 'grp-audio' },
                    { deviceId: 'cam-1',   kind: 'videoinput',  label: '', groupId: 'grp-video' }
                ]);
            });
        }
    } catch (e) {}

    // iOS/Safari has no Client Hints — drop userAgentData when emulating Apple devices.
    try {
        if (/iPhone|iPad/i.test(ua) && 'userAgentData' in navigator) {
            defineNativeGetter(navigator, 'userAgentData', undefined);
        }
    } catch (e) {}
})();
";
}
