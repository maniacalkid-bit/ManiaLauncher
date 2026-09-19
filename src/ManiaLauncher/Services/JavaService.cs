using System.Diagnostics;
using System.IO;
using System.Text.RegularExpressions;

namespace ManiaLauncher.Services;

public sealed record JavaInstall(string Path, int MajorVersion, string Source);

/// <summary>
/// Discovers installed Java runtimes so the user can pick one, and validates
/// that a version appropriate for the selected Minecraft release exists.
/// </summary>
public sealed partial class JavaService
{
    private static readonly Lazy<JavaService> Lazy = new(() => new JavaService());
    public static JavaService Instance => Lazy.Value;

    private JavaService() { }

    public List<JavaInstall> Installs { get; private set; } = new();

    public async Task ScanAsync()
    {
        var found = new List<JavaInstall>();
        var seenPaths = new HashSet<string>(StringComparer.OrdinalIgnoreCase);

        void TryAdd(string? candidate, string source)
        {
            if (string.IsNullOrWhiteSpace(candidate)) return;
            var exe = Path.Combine(candidate, "bin", "java.exe");
            if (!File.Exists(exe)) return;
            if (!seenPaths.Add(Path.GetFullPath(exe))) return;
            found.Add(new JavaInstall(exe, 0, source));
        }

        // 1) JAVA_HOME
        TryAdd(Environment.GetEnvironmentVariable("JAVA_HOME"), "JAVA_HOME");

        // 2) PATH
        foreach (var dir in (Environment.GetEnvironmentVariable("PATH") ?? "").Split(';'))
            TryAdd(dir.Trim(), "PATH");

        // 3) Common install locations
        string[] roots =
        [
            @"C:\Program Files\Java",
            @"C:\Program Files (x86)\Java",
            @"C:\Program Files\Eclipse Adoptium",
            @"C:\Program Files\Microsoft",
            @"C:\Program Files\Amazon Corretto",
            @"C:\Program Files\Zulu",
            @"C:\Program Files\BellSoft",
            @"C:\Program Files\OpenJDK",
            @"C:\Program Files (x86)\Minecraft Launcher\runtime",
        ];
        foreach (var root in roots)
        {
            if (!Directory.Exists(root)) continue;
            try
            {
                foreach (var sub in Directory.GetDirectories(root))
                {
                    TryAdd(sub, Path.GetFileName(root));
                    // Minecraft Launcher runtime layout: runtime\java-runtime-gamma\windows-x64\...
                    var winDir = Directory.GetDirectories(sub, "windows-x64", SearchOption.AllDirectories)
                        .FirstOrDefault();
                    TryAdd(winDir, Path.GetFileName(sub));
                }
            }
            catch { /* ignore */ }
        }

        // Probe each candidate for its real major version (parallel, timeout-protected).
        var probes = found.Select(async f =>
        {
            var ver = await ProbeVersionAsync(f.Path);
            return f with { MajorVersion = ver };
        });
        Installs = (await Task.WhenAll(probes))
            .Where(f => f.MajorVersion > 0)
            .GroupBy(f => f.Path, StringComparer.OrdinalIgnoreCase)
            .Select(g => g.OrderByDescending(f => f.MajorVersion).First())
            .OrderByDescending(f => f.MajorVersion)
            .ToList();

        LogService.Instance.Info($"Java scan found {Installs.Count} install(s): " +
            string.Join(", ", Installs.Select(i => $"{i.MajorVersion}@{i.Path}")));
    }

    public static async Task<int> ProbeVersionAsync(string javaExe)
    {
        try
        {
            using var p = new Process();
            p.StartInfo = new ProcessStartInfo
            {
                FileName = javaExe,
                Arguments = "-version",
                UseShellExecute = false,
                RedirectStandardError = true,
                RedirectStandardOutput = true,
                CreateNoWindow = true
            };
            p.Start();
            var err = await p.StandardError.ReadToEndAsync();
            var outp = await p.StandardOutput.ReadToEndAsync();
            using var cts = new CancellationTokenSource(TimeSpan.FromSeconds(10));
            await p.WaitForExitAsync(cts.Token);
            var text = err + outp;
            return ParseMajorVersion(text);
        }
        catch (Exception ex)
        {
            LogService.Instance.Warn($"Java probe failed for '{javaExe}': {ex.Message}");
            return 0;
        }
    }

    [GeneratedRegex(@"(?:version|openjdk version)\s+""?(\d+)", RegexOptions.IgnoreCase)]
    private static partial Regex VersionRegex();

    public static int ParseMajorVersion(string text)
    {
        var m = VersionRegex().Match(text);
        if (!m.Success) return 0;
        var major = int.Parse(m.Groups[1].Value);
        // "1.8.0_xxx" style: legacy numbering
        if (major == 1)
        {
            var m2 = VersionRegex().Match(text);
            var parts = text.Split('"').Where(s => s.Contains('.')).FirstOrDefault();
            var seg = parts?.Split('.');
            if (seg is { Length: > 1 } && int.TryParse(seg[1], out var legacy)) return legacy;
            _ = m2;
        }
        return major;
    }

    /// <summary>
    /// Picks the best java for a Minecraft version: 1.20.5+ needs Java 21,
    /// 1.17+ needs Java 17, older accepts Java 8+.
    /// </summary>
    public JavaInstall? GetBestForVersion(string mcVersionName)
    {
        int needed = mcVersionName switch
        {
            var n when IsAtLeast(n, 1, 20, 5) => 21,
            var n when IsAtLeast(n, 1, 17) => 17,
            _ => 8
        };
        return Installs.FirstOrDefault(i => i.MajorVersion == needed)
            ?? Installs.FirstOrDefault(i => i.MajorVersion >= needed)
            ?? Installs.FirstOrDefault();
    }

    public static bool IsAtLeast(string mcVersion, int major, int minor, int patch = 0)
    {
        // Handles "1.21.4", "26.1" (new numbering), snapshots ("24w14a" -> treat as latest).
        if (string.IsNullOrWhiteSpace(mcVersion)) return false;
        if (mcVersion.Contains('w') && char.IsDigit(mcVersion[0])) return true; // snapshot of current dev cycle
        var nums = mcVersion.Split('.');
        if (nums.Length == 0 || !int.TryParse(nums[0], out var mj)) return false;
        if (mj > major) return true;
        if (mj < major) return false;
        if (nums.Length > 1 && int.TryParse(nums[1], out var mn))
        {
            if (mn > minor) return true;
            if (mn < minor) return false;
        }
        else return minor == 0;
        if (nums.Length > 2 && int.TryParse(nums[2], out var pt)) return pt >= patch;
        return patch == 0;
    }
}
