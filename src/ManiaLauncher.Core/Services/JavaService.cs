using System.Diagnostics;
using System.Text.RegularExpressions;

namespace ManiaLauncher.Core.Services;

public class JavaService
{
    private readonly string _javaRoot;

    public JavaService(string launcherDirectory)
    {
        _javaRoot = Path.Combine(launcherDirectory, "java");
        Directory.CreateDirectory(_javaRoot);
    }

    public string? FindJava()
    {
        var localJava = Path.Combine(_javaRoot, "bin", "java.exe");
        if (File.Exists(localJava)) return localJava;

        var javaHome = Environment.GetEnvironmentVariable("JAVA_HOME");
        if (!string.IsNullOrEmpty(javaHome))
        {
            var path = Path.Combine(javaHome, "bin", "java.exe");
            if (File.Exists(path)) return path;
        }

        // Типичные пути Adoptium / Oracle
        var commonPaths = new[]
        {
            @"C:\Program Files\Eclipse Adoptium",
            @"C:\Program Files\Java",
            @"C:\Program Files\Microsoft",
            @"C:\Program Files\Amazon Corretto"
        };

        foreach (var root in commonPaths)
        {
            if (!Directory.Exists(root)) continue;
            try
            {
                foreach (var dir in Directory.GetDirectories(root))
                {
                    var javaExe = Path.Combine(dir, "bin", "java.exe");
                    if (File.Exists(javaExe)) return javaExe;
                }
            }
            catch { }
        }

        try
        {
            var psi = new ProcessStartInfo
            {
                FileName = "java",
                Arguments = "-version",
                RedirectStandardError = true,
                UseShellExecute = false,
                CreateNoWindow = true
            };
            using var p = Process.Start(psi);
            if (p != null)
            {
                p.WaitForExit(3000);
                if (p.ExitCode == 0) return "java";
            }
        }
        catch { }

        return null;
    }

    public Task<string> EnsureJavaAsync(IProgress<string>? progress = null, CancellationToken ct = default)
    {
        var existing = FindJava();
        if (existing != null)
        {
            progress?.Report($"Найдена Java: {existing}");
            return Task.FromResult(existing);
        }

        progress?.Report("Java не найдена");
        throw new Exception(
            "Java не найдена.\n\n" +
            "Установи Java 21 (Temurin) с сайта:\n" +
            "https://adoptium.net/\n\n" +
            "После установки перезапусти лаунчер.");
    }

    public string GetJavaVersion(string javaPath)
    {
        try
        {
            var psi = new ProcessStartInfo
            {
                FileName = javaPath,
                Arguments = "-version",
                RedirectStandardError = true,
                UseShellExecute = false,
                CreateNoWindow = true
            };
            using var p = Process.Start(psi);
            if (p == null) return "unknown";
            var output = p.StandardError.ReadToEnd();
            p.WaitForExit();

            var match = Regex.Match(output, @"version ""([\d._]+)""");
            return match.Success ? match.Groups[1].Value : "unknown";
        }
        catch
        {
            return "unknown";
        }
    }
}
