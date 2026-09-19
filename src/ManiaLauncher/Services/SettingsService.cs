using System.ComponentModel;
using System.IO;
using System.Runtime.CompilerServices;
using System.Text.Json;
using System.Text.Json.Serialization;

namespace ManiaLauncher.Services;

/// <summary>
/// Application settings persisted as JSON in %APPDATA%\ManiaLauncher\settings.json.
/// </summary>
public sealed class SettingsService : INotifyPropertyChanged
{
    private static readonly Lazy<SettingsService> Lazy = new(() => new SettingsService());
    public static SettingsService Instance => Lazy.Value;

    private static string FilePath => Path.Combine(AppInfo.AppDataDir, "settings.json");

    private static readonly JsonSerializerOptions JsonOptions = new()
    {
        WriteIndented = true,
        DefaultIgnoreCondition = JsonIgnoreCondition.WhenWritingNull
    };

    public event PropertyChangedEventHandler? PropertyChanged;

    private SettingsService()
    {
        Load();
    }

    // ---- persisted properties ----

    private string _gameDirectory = DefaultGameDirectory();
    /// <summary>Root directory for game files (.minecraft-style layout).</summary>
    public string GameDirectory
    {
        get => _gameDirectory;
        set { _gameDirectory = value; Save(); OnChanged(); }
    }

    private string? _lastVersion;
    public string? LastVersion
    {
        get => _lastVersion;
        set { _lastVersion = value; Save(); OnChanged(); }
    }

    private string? _lastAccount;
    public string? LastAccount
    {
        get => _lastAccount;
        set { _lastAccount = value; Save(); OnChanged(); }
    }

    private int _maxRamMb = 2048;
    public int MaxRamMb
    {
        get => _maxRamMb;
        set { _maxRamMb = Math.Clamp(value, 512, 64 * 1024); Save(); OnChanged(); }
    }

    private string? _javaPath;
    /// <summary>Custom path to java(.exe). Null = auto-detect.</summary>
    public string? JavaPath
    {
        get => _javaPath;
        set { _javaPath = value; Save(); OnChanged(); }
    }

    private bool _showSnapshots;
    public bool ShowSnapshots
    {
        get => _showSnapshots;
        set { _showSnapshots = value; Save(); OnChanged(); }
    }

    private bool _showOldVersions;
    public bool ShowOldVersions
    {
        get => _showOldVersions;
        set { _showOldVersions = value; Save(); OnChanged(); }
    }

    private bool _closeLauncherOnStart = true;
    public bool CloseLauncherOnStart
    {
        get => _closeLauncherOnStart;
        set { _closeLauncherOnStart = value; Save(); OnChanged(); }
    }

    private List<string> _dismissedAds = new();
    /// <summary>Fingerprints of promotional banners the user has closed.</summary>
    public IReadOnlyList<string> DismissedAds => _dismissedAds;

    public bool IsAdDismissed(string fingerprint) =>
        _dismissedAds.Contains(fingerprint, StringComparer.Ordinal);

    public void AddDismissedAd(string fingerprint)
    {
        if (string.IsNullOrWhiteSpace(fingerprint)) return;
        if (_dismissedAds.Contains(fingerprint, StringComparer.Ordinal)) return;
        _dismissedAds.Add(fingerprint);
        Save();
    }

    private int _windowWidth = 1000;
    public int WindowWidth
    {
        get => _windowWidth;
        set { _windowWidth = value; Save(); OnChanged(); }
    }

    private int _windowHeight = 640;
    public int WindowHeight
    {
        get => _windowHeight;
        set { _windowHeight = value; Save(); OnChanged(); }
    }

    public static string DefaultGameDirectory()
    {
        // Honor portable/overridden data dir when available; fall back to %APPDATA%.
        if (!string.IsNullOrEmpty(AppInfo.AppDataDir))
            return Path.Combine(AppInfo.AppDataDir, "game");
        var appData = Environment.GetFolderPath(Environment.SpecialFolder.ApplicationData);
        return Path.Combine(appData, "ManiaLauncher", "game");
    }

    public void Load()
    {
        try
        {
            if (!File.Exists(FilePath)) return;
            var dto = JsonSerializer.Deserialize<SettingsDto>(File.ReadAllText(FilePath), JsonOptions);
            if (dto == null) return;

            _gameDirectory = string.IsNullOrWhiteSpace(dto.GameDirectory) ? DefaultGameDirectory() : dto.GameDirectory;
            _lastVersion = dto.LastVersion;
            _lastAccount = dto.LastAccount;
            _maxRamMb = dto.MaxRamMb is > 0 ? dto.MaxRamMb.Value : 2048;
            _javaPath = dto.JavaPath;
            _showSnapshots = dto.ShowSnapshots ?? false;
            _showOldVersions = dto.ShowOldVersions ?? false;
            _closeLauncherOnStart = dto.CloseLauncherOnStart ?? true;
            _windowWidth = dto.WindowWidth is > 200 ? dto.WindowWidth.Value : 1000;
            _windowHeight = dto.WindowHeight is > 200 ? dto.WindowHeight.Value : 640;
            _dismissedAds = dto.DismissedAds ?? new List<string>();
        }
        catch (Exception ex)
        {
            LogService.Instance.Error($"Failed to load settings: {ex.Message}");
        }
    }

    public void Save()
    {
        try
        {
            var dto = new SettingsDto
            {
                GameDirectory = _gameDirectory,
                LastVersion = _lastVersion,
                LastAccount = _lastAccount,
                MaxRamMb = _maxRamMb,
                JavaPath = _javaPath,
                ShowSnapshots = _showSnapshots,
                ShowOldVersions = _showOldVersions,
                CloseLauncherOnStart = _closeLauncherOnStart,
                WindowWidth = _windowWidth,
                WindowHeight = _windowHeight,
                DismissedAds = _dismissedAds
            };
            Directory.CreateDirectory(AppInfo.AppDataDir);
            File.WriteAllText(FilePath, JsonSerializer.Serialize(dto, JsonOptions));
        }
        catch (Exception ex)
        {
            LogService.Instance.Error($"Failed to save settings: {ex.Message}");
        }
    }

    private void OnChanged([CallerMemberName] string? name = null) =>
        PropertyChanged?.Invoke(this, new PropertyChangedEventArgs(name));

    private sealed class SettingsDto
    {
        public string? GameDirectory { get; set; }
        public string? LastVersion { get; set; }
        public string? LastAccount { get; set; }
        public int? MaxRamMb { get; set; }
        public string? JavaPath { get; set; }
        public bool? ShowSnapshots { get; set; }
        public bool? ShowOldVersions { get; set; }
        public bool? CloseLauncherOnStart { get; set; }
        public int? WindowWidth { get; set; }
        public int? WindowHeight { get; set; }
        public List<string>? DismissedAds { get; set; }
    }
}
