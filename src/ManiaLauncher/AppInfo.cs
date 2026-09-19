using System.IO;
using System.Reflection;

namespace ManiaLauncher;

/// <summary>
/// Static branding and path information for the launcher.
/// </summary>
public static class AppInfo
{
    public const string Brand = "Mania Launcher";
    public const string Owner = "maniacalkid";
    public const string Title = "Mania Launcher";

    /// <summary>
    /// Remote raw JSON that controls the promotional banner on the Home page.
    /// Edit that file in the repository to change the banner for every user —
    /// no launcher update required.
    /// </summary>
    public const string AdsUrl =
        "https://raw.githubusercontent.com/maniacalkid-bit/mania-ads/refs/heads/main/ads.json";

    public static string Version { get; private set; } = "1.0.0";

    /// <summary>%APPDATA%\ManiaLauncher</summary>
    public static string AppDataDir { get; private set; } = "";

    /// <summary>%APPDATA%\ManiaLauncher\logs</summary>
    public static string LogsDir { get; private set; } = "";

    public static void Init()
    {
        Version = Assembly.GetExecutingAssembly().GetName().Version?.ToString(3) ?? "1.0.0";

        // Portable mode: MANIA_DATA_DIR overrides %APPDATA%\ManiaLauncher.
        var custom = Environment.GetEnvironmentVariable("MANIA_DATA_DIR");
        AppDataDir = !string.IsNullOrWhiteSpace(custom)
            ? custom
            : Path.Combine(
                Environment.GetFolderPath(Environment.SpecialFolder.ApplicationData),
                "ManiaLauncher");

        LogsDir = Path.Combine(AppDataDir, "logs");
        Directory.CreateDirectory(AppDataDir);
        Directory.CreateDirectory(LogsDir);
    }
}
