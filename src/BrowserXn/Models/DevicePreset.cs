namespace GDD.Models;

public sealed record DevicePreset(
    string Name,
    string Category,
    int Width,
    int Height,
    double DeviceScaleFactor,
    string UserAgent,
    bool IsMobile = true,
    bool HasTouch = true);

public static class DevicePresets
{
    private const string CatPhone = "Phone";
    private const string CatTablet = "Tablet";
    private const string CatDesktop = "Desktop";

    private const string UaIOS = "Mozilla/5.0 (iPhone; CPU iPhone OS 18_0 like Mac OS X) AppleWebKit/605.1.15 (KHTML, like Gecko) Version/18.0 Mobile/15E148 Safari/604.1";
    private const string UaAndroid = "Mozilla/5.0 (Linux; Android 15; Pixel 9) AppleWebKit/537.36 (KHTML, like Gecko) Chrome/128.0.0.0 Mobile Safari/537.36";
    private const string UaIPad = "Mozilla/5.0 (iPad; CPU OS 18_0 like Mac OS X) AppleWebKit/605.1.15 (KHTML, like Gecko) Version/18.0 Safari/604.1";
    private const string UaAndroidTab = "Mozilla/5.0 (Linux; Android 15; Pixel Tablet) AppleWebKit/537.36 (KHTML, like Gecko) Chrome/128.0.0.0 Safari/537.36";
    private const string UaDesktop = "Mozilla/5.0 (Windows NT 10.0; Win64; x64) AppleWebKit/537.36 (KHTML, like Gecko) Chrome/128.0.0.0 Safari/537.36";
    private const string UaMac = "Mozilla/5.0 (Macintosh; Intel Mac OS X 14_0) AppleWebKit/605.1.15 (KHTML, like Gecko) Version/18.0 Safari/605.1.15";

    // --- Phones ---
    public static readonly DevicePreset IPhoneSE = new("iPhone SE", CatPhone, 375, 667, 2.0, UaIOS);
    public static readonly DevicePreset IPhone14 = new("iPhone 14", CatPhone, 390, 844, 3.0, UaIOS);
    public static readonly DevicePreset IPhone15Pro = new("iPhone 15 Pro", CatPhone, 393, 852, 3.0, UaIOS);
    public static readonly DevicePreset IPhone15ProMax = new("iPhone 15 Pro Max", CatPhone, 430, 932, 3.0, UaIOS);
    public static readonly DevicePreset IPhone16Pro = new("iPhone 16 Pro", CatPhone, 402, 874, 3.0, UaIOS);
    public static readonly DevicePreset IPhone16ProMax = new("iPhone 16 Pro Max", CatPhone, 440, 956, 3.0, UaIOS);
    public static readonly DevicePreset Pixel9 = new("Pixel 9", CatPhone, 412, 915, 2.625, UaAndroid);
    public static readonly DevicePreset Pixel9Pro = new("Pixel 9 Pro", CatPhone, 412, 915, 2.625, UaAndroid);
    public static readonly DevicePreset GalaxyS24 = new("Galaxy S24", CatPhone, 360, 780, 3.0, UaAndroid);
    public static readonly DevicePreset GalaxyS24Ultra = new("Galaxy S24 Ultra", CatPhone, 412, 915, 3.0, UaAndroid);
    public static readonly DevicePreset OnePlus12 = new("OnePlus 12", CatPhone, 412, 915, 3.5, UaAndroid);

    // --- Tablets ---
    public static readonly DevicePreset IPadMini = new("iPad Mini", CatTablet, 744, 1133, 2.0, UaIPad, IsMobile: false);
    public static readonly DevicePreset IPadAir = new("iPad Air", CatTablet, 820, 1180, 2.0, UaIPad, IsMobile: false);
    public static readonly DevicePreset IPadPro11 = new("iPad Pro 11\"", CatTablet, 834, 1194, 2.0, UaIPad, IsMobile: false);
    public static readonly DevicePreset IPadPro13 = new("iPad Pro 13\"", CatTablet, 1024, 1366, 2.0, UaIPad, IsMobile: false);
    public static readonly DevicePreset GalaxyTabS9 = new("Galaxy Tab S9", CatTablet, 800, 1280, 2.0, UaAndroidTab, IsMobile: false);
    public static readonly DevicePreset PixelTablet = new("Pixel Tablet", CatTablet, 800, 1280, 2.0, UaAndroidTab, IsMobile: false);

    // --- Desktops ---
    public static readonly DevicePreset Laptop = new("Laptop HD", CatDesktop, 1366, 768, 1.0, UaDesktop, IsMobile: false, HasTouch: false);
    public static readonly DevicePreset LaptopHiDPI = new("Laptop HiDPI", CatDesktop, 1440, 900, 2.0, UaMac, IsMobile: false, HasTouch: false);
    public static readonly DevicePreset Desktop1080 = new("Desktop 1080p", CatDesktop, 1920, 1080, 1.0, UaDesktop, IsMobile: false, HasTouch: false);
    public static readonly DevicePreset Desktop1440 = new("Desktop 1440p", CatDesktop, 2560, 1440, 1.0, UaDesktop, IsMobile: false, HasTouch: false);
    public static readonly DevicePreset Desktop4K = new("Desktop 4K", CatDesktop, 3840, 2160, 2.0, UaDesktop, IsMobile: false, HasTouch: false);

    public static IReadOnlyList<DevicePreset> All { get; } = new[]
    {
        IPhoneSE, IPhone14, IPhone15Pro, IPhone15ProMax, IPhone16Pro, IPhone16ProMax,
        Pixel9, Pixel9Pro, GalaxyS24, GalaxyS24Ultra, OnePlus12,
        IPadMini, IPadAir, IPadPro11, IPadPro13, GalaxyTabS9, PixelTablet,
        Laptop, LaptopHiDPI, Desktop1080, Desktop1440, Desktop4K
    };

    public static IReadOnlyList<DevicePreset> Phones { get; } = All.Where(d => d.Category == CatPhone).ToList();
    public static IReadOnlyList<DevicePreset> Tablets { get; } = All.Where(d => d.Category == CatTablet).ToList();
    public static IReadOnlyList<DevicePreset> Desktops { get; } = All.Where(d => d.Category == CatDesktop).ToList();

    public static DevicePreset Default => IPhone15Pro;
}

public sealed record DeviceTestPreset(string Name, DevicePreset[] Devices)
{
    public static readonly DeviceTestPreset PhoneBasic = new("3 Phones", new[]
    {
        DevicePresets.IPhoneSE, DevicePresets.IPhone15Pro, DevicePresets.Pixel9
    });

    public static readonly DeviceTestPreset PhoneFull = new("All Phones", DevicePresets.Phones.ToArray());

    public static readonly DeviceTestPreset Responsive = new("Responsive (Phone + Tablet + Desktop)", new[]
    {
        DevicePresets.IPhone15Pro, DevicePresets.IPadAir, DevicePresets.Desktop1080
    });

    public static readonly DeviceTestPreset CrossPlatform = new("Cross-Platform", new[]
    {
        DevicePresets.IPhoneSE, DevicePresets.IPhone16ProMax,
        DevicePresets.Pixel9, DevicePresets.GalaxyS24Ultra,
        DevicePresets.IPadAir, DevicePresets.Desktop1080
    });

    public static readonly DeviceTestPreset AllScreens = new("All Screens", new[]
    {
        DevicePresets.IPhoneSE, DevicePresets.IPhone15Pro, DevicePresets.IPhone16ProMax,
        DevicePresets.Pixel9, DevicePresets.GalaxyS24Ultra,
        DevicePresets.IPadMini, DevicePresets.IPadPro13,
        DevicePresets.Laptop, DevicePresets.Desktop1080, DevicePresets.Desktop4K
    });

    public static IReadOnlyList<DeviceTestPreset> All { get; } = new[]
    {
        PhoneBasic, PhoneFull, Responsive, CrossPlatform, AllScreens
    };
}
