using CmlLib.Core;
using CmlLib.Core.VersionMetadata;
using System.ComponentModel;
using System.IO;
using System.Runtime.CompilerServices;

namespace ManiaLauncher.Services;

/// <summary>
/// UI-facing wrapper around a game version.
/// </summary>
public sealed class GameVersionItem : INotifyPropertyChanged
{
    public required IVersionMetadata Metadata { get; init; }

    public string Name => Metadata.Name;
    public string TypeRaw => Metadata.Type ?? "";
    public DateTimeOffset ReleaseTime => Metadata.ReleaseTime;

    public MVersionType Type => Metadata.GetVersionType();

    public bool IsRelease => Type == MVersionType.Release;
    public bool IsSnapshot => Type == MVersionType.Snapshot;
    public bool IsOld => Type == MVersionType.OldAlpha || Type == MVersionType.OldBeta;
    public bool IsCustom => Type == MVersionType.Custom;

    public string TypeLabel => Type switch
    {
        MVersionType.Release => "release",
        MVersionType.Snapshot => "snapshot",
        MVersionType.OldBeta => "old beta",
        MVersionType.OldAlpha => "old alpha",
        _ => "modded"
    };

    public bool IsInstalled { get => _isInstalled; set { _isInstalled = value; OnChanged(); } }
    private bool _isInstalled;

    public string ReleaseDateLabel =>
        ReleaseTime == default ? "" : ReleaseTime.LocalDateTime.ToString("yyyy-MM-dd");

    public event PropertyChangedEventHandler? PropertyChanged;
    private void OnChanged([CallerMemberName] string? n = null) =>
        PropertyChanged?.Invoke(this, new PropertyChangedEventArgs(n));

    public override string ToString() => Name;
}

/// <summary>
/// Loads the version list from the official Mojang manifest via CmlLib,
/// merges locally installed versions, and detects newly released versions.
/// </summary>
public sealed class VersionService
{
    private static readonly Lazy<VersionService> Lazy = new(() => new VersionService());
    public static VersionService Instance => Lazy.Value;

    private VersionService() { }

    public List<GameVersionItem> Versions { get; private set; } = new();
    public string? LatestRelease { get; private set; }
    public string? LatestSnapshot { get; private set; }

    /// <summary>Raised when the Mojang manifest contains a version we have not seen before.</summary>
    public event Action<string>? NewVersionDetected;

    private readonly object _seenLock = new();
    private HashSet<string> _seenVersions = new();

    /// <summary>
    /// Fetches all versions (remote manifest + local installs).
    /// </summary>
    public async Task LoadAsync(MinecraftLauncher launcher, CancellationToken ct = default)
    {
        var remote = await launcher.GetAllVersionsAsync(ct);
        LatestRelease = remote.LatestReleaseName;
        LatestSnapshot = remote.LatestSnapshotName;

        var installed = GetInstalledIds(launcher.MinecraftPath);

        var list = new List<GameVersionItem>();
        foreach (var meta in remote)
        {
            var item = new GameVersionItem { Metadata = meta };
            item.IsInstalled = installed.Contains(item.Name);
            list.Add(item);
        }

        // Local versions that are not in the remote manifest (e.g. modded installs).
        foreach (var id in installed)
        {
            if (list.Any(v => v.Name == id)) continue;
            try
            {
                var localMeta = new LocalVersionMetadata(
                    new JsonVersionMetadataModel { Type = "custom", ReleaseTime = default },
                    launcher.MinecraftPath.GetVersionJsonPath(id));
                list.Add(new GameVersionItem { Metadata = localMeta, IsInstalled = true });
            }
            catch (Exception ex)
            {
                LogService.Instance.Warn($"Could not wrap local version '{id}': {ex.Message}");
            }
        }

        Versions = list;
        DetectNewVersions(list);
        LogService.Instance.Info($"Loaded {list.Count} versions (release={LatestRelease}, snapshot={LatestSnapshot})");
    }

    /// <summary>Ids of versions physically present in the versions folder.</summary>
    public HashSet<string> GetInstalledIds(CmlLib.Core.MinecraftPath path)
    {
        var ids = new HashSet<string>(StringComparer.OrdinalIgnoreCase);
        try
        {
            var dir = new DirectoryInfo(path.Versions);
            if (dir.Exists)
                foreach (var sub in dir.GetDirectories())
                    if (File.Exists(path.GetVersionJsonPath(sub.Name)))
                        ids.Add(sub.Name);
        }
        catch (Exception ex)
        {
            LogService.Instance.Warn($"Installed-scan failed: {ex.Message}");
        }
        return ids;
    }

    private void DetectNewVersions(List<GameVersionItem> list)
    {
        lock (_seenLock)
        {
            if (_seenVersions.Count == 0)
            {
                // First load: remember everything, don't spam "new version".
                foreach (var v in list) _seenVersions.Add(v.Name);
                return;
            }

            foreach (var v in list)
            {
                if (!_seenVersions.Contains(v.Name))
                {
                    _seenVersions.Add(v.Name);
                    NewVersionDetected?.Invoke(v.Name);
                    LogService.Instance.Info($"New version available from Mojang: {v.Name}");
                }
            }
        }
    }

    /// <summary>Applies user filters (snapshots / old versions) to the loaded list.</summary>
    public IEnumerable<GameVersionItem> Filter(bool showSnapshots, bool showOldVersions)
    {
        IEnumerable<GameVersionItem> q = Versions;

        q = q.Where(v => v.IsRelease || v.IsCustom || (showSnapshots && v.IsSnapshot) || (showOldVersions && v.IsOld));

        // Sort: releases first by date desc, then snapshots, then old, then custom.
        return q
            .OrderByDescending(v => v.IsRelease)
            .ThenByDescending(v => v.ReleaseTime)
            .ThenBy(v => v.Name);
    }
}
