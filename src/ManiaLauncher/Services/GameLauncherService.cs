using CmlLib.Core;
using CmlLib.Core.Auth;
using CmlLib.Core.Installers;
using CmlLib.Core.ProcessBuilder;
using System.Diagnostics;
using System.IO;
using System.Net.Http;

namespace ManiaLauncher.Services;

public sealed record LaunchProgress(double FileRatio, double ByteRatio, string CurrentFile, int FilesDone, int FilesTotal);

/// <summary>
/// Bridges the UI with CmlLib.Core: creates the launcher bound to the game
/// directory, installs versions with progress reporting, and starts the game.
/// </summary>
public sealed class GameLauncherService
{
    private static readonly Lazy<GameLauncherService> Lazy = new(() => new GameLauncherService());
    public static GameLauncherService Instance => Lazy.Value;

    private MinecraftLauncher? _launcher;
    private string _boundPath = "";

    private GameLauncherService() { }

    public event Action<LaunchProgress>? ProgressChanged;
    public event Action<string>? StatusChanged;

    public MinecraftLauncher GetLauncher()
    {
        var gameDir = SettingsService.Instance.GameDirectory;
        if (_launcher != null && string.Equals(_boundPath, gameDir, StringComparison.OrdinalIgnoreCase))
            return _launcher;

        Directory.CreateDirectory(gameDir);
        var path = new MinecraftPath(gameDir);
        path.CreateDirs();

        var httpClient = new HttpClient(new HttpClientHandler
        {
            AutomaticDecompression = System.Net.DecompressionMethods.All
        })
        {
            Timeout = TimeSpan.FromSeconds(60)
        };
        httpClient.DefaultRequestHeaders.UserAgent.ParseAdd($"{AppInfo.Brand}/{AppInfo.Version}");

        var parameters = MinecraftLauncherParameters.CreateDefault(path, httpClient);
        parameters.GameInstaller = ParallelGameInstaller.CreateAsCoreCount(httpClient);

        _launcher = new MinecraftLauncher(parameters);
        _launcher.FileProgressChanged += OnFileProgress;
        _launcher.ByteProgressChanged += OnByteProgress;
        _boundPath = gameDir;

        LogService.Instance.Info($"Launcher bound to game dir: {gameDir}");
        return _launcher;
    }

    private double _lastFileRatio, _lastByteRatio;
    private string _lastFile = "";
    private int _filesDone, _filesTotal;

    private void OnFileProgress(object? sender, InstallerProgressChangedEventArgs e)
    {
        _filesDone = e.ProgressedTasks;
        _filesTotal = Math.Max(1, e.TotalTasks);
        _lastFileRatio = (double)_filesDone / _filesTotal;
        _lastFile = e.Name ?? "";
        Raise();
    }

    private void OnByteProgress(object? sender, ByteProgress e)
    {
        _lastByteRatio = e.ToRatio();
        Raise();
    }

    private void Raise() =>
        ProgressChanged?.Invoke(new LaunchProgress(_lastFileRatio, _lastByteRatio, _lastFile, _filesDone, _filesTotal));

    /// <summary>Downloads/updates a version and starts the game. Returns the process.</summary>
    public async Task<Process> InstallAndLaunchAsync(string versionName, OfflineAccount account, CancellationToken ct)
    {
        var launcher = GetLauncher();

        StatusChanged?.Invoke($"Подготовка {versionName}…");
        var version = await launcher.GetVersionAsync(versionName, ct);

        // Java selection: custom path wins, otherwise best match for the version.
        string? javaPath = SettingsService.Instance.JavaPath;
        if (string.IsNullOrWhiteSpace(javaPath) || !File.Exists(javaPath))
        {
            javaPath = JavaService.Instance.GetBestForVersion(versionName)?.Path;
        }

        // Fall back to the java bundled/downloaded by CmlLib for this version.
        if (string.IsNullOrWhiteSpace(javaPath) || !File.Exists(javaPath))
        {
            try { javaPath = launcher.GetJavaPath(version); } catch { /* ignore */ }
        }
        if (string.IsNullOrWhiteSpace(javaPath) || !File.Exists(javaPath))
        {
            try { javaPath = launcher.GetDefaultJavaPath(); } catch { /* ignore */ }
        }

        if (string.IsNullOrWhiteSpace(javaPath) || !File.Exists(javaPath))
            throw new FileNotFoundException(
                "Java не найдена. Установите Java 21 (рекомендуется) или укажите свой путь к java в настройках.");

        var session = MSession.CreateOfflineSession(account.Username);

        StatusChanged?.Invoke($"Установка {versionName}…");
        await launcher.InstallAsync(version, ct);

        StatusChanged?.Invoke($"Запуск {versionName}…");
        var options = new MLaunchOption
        {
            Session = session,
            StartVersion = version,
            Path = launcher.MinecraftPath,
            JavaPath = javaPath,
            MaximumRamMb = SettingsService.Instance.MaxRamMb,
            VersionType = AppInfo.Brand,
            GameLauncherName = AppInfo.Brand.Replace(" ", ""),
            GameLauncherVersion = AppInfo.Version
        };

        var process = launcher.BuildProcess(version, options);
        process.EnableRaisingEvents = true;
        process.Start();

        LogService.Instance.Info(
            $"Launched {versionName} as {account.Username} (pid={process.Id}, java={javaPath})");
        StatusChanged?.Invoke($"Игра запущена: {versionName}");
        return process;
    }
}
