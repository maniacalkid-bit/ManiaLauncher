using ManiaLauncher.Core.Models;
using Newtonsoft.Json.Linq;
using System.Diagnostics;
using System.IO.Compression;
using System.Text;

namespace ManiaLauncher.Core.Services;

public class LaunchService
{
    private readonly string _gameDirectory;
    private readonly string _librariesPath;

    public LaunchService(string gameDirectory)
    {
        _gameDirectory = gameDirectory;
        _librariesPath = Path.Combine(gameDirectory, "libraries");
        Directory.CreateDirectory(_gameDirectory);
    }

    public Process Launch(Profile profile, Account account, string javaPath, IProgress<string>? log = null)
    {
        var versionDir = Path.Combine(_gameDirectory, "versions", profile.VersionId);
        var jarPath = Path.Combine(versionDir, $"{profile.VersionId}.jar");
        var jsonPath = Path.Combine(versionDir, $"{profile.VersionId}.json");

        if (!File.Exists(jarPath) || !File.Exists(jsonPath))
        {
            throw new FileNotFoundException(
                $"Версия {profile.VersionId} не установлена.\n\n" +
                "Перейди во вкладку «Версии», выбери нужную версию\n" +
                "и нажми «Установить выбранную».");
        }

        var versionJson = File.ReadAllText(jsonPath);
        var root = JObject.Parse(versionJson);

        // ─── 1. Собираем classpath (все libraries + client.jar) ───
        var classpath = new List<string>();

        var libraries = root["libraries"] as JArray;
        if (libraries != null)
        {
            foreach (var lib in libraries)
            {
                if (!IsLibraryAllowed(lib)) continue;

                // Обычная artifact-библиотека
                var artifact = lib["downloads"]?["artifact"];
                if (artifact != null)
                {
                    var libPath = artifact["path"]?.ToString();
                    if (!string.IsNullOrEmpty(libPath))
                    {
                        var full = Path.Combine(_librariesPath, libPath.Replace('/', Path.DirectorySeparatorChar));
                        if (File.Exists(full))
                            classpath.Add(full);
                    }
                }
            }
        }

        classpath.Add(jarPath); // client.jar в конце

        // ─── 2. Распаковываем natives (LWJGL и т.д.) ───
        var nativesDir = Path.Combine(versionDir, "natives");
        Directory.CreateDirectory(nativesDir);
        ExtractNatives(root, nativesDir, log);

        // ─── 3. Asset index ───
        var assetIndex = root["assetIndex"]?["id"]?.ToString() ?? profile.VersionId;
        var assetsDir = Path.Combine(_gameDirectory, "assets");

        // ─── 4. Главный класс ───
        var mainClass = root["mainClass"]?.ToString() ?? "net.minecraft.client.main.Main";

        // ─── 5. Собираем аргументы ───
        var cp = string.Join(Path.PathSeparator.ToString(), classpath);

        var args = new StringBuilder();
        args.Append(profile.JvmArguments).Append(' ');
        args.Append($"-Djava.library.path=\"{nativesDir}\" ");
        args.Append($"-Dminecraft.launcher.brand=ManiaLauncher ");
        args.Append($"-Dminecraft.launcher.version=1.0 ");
        args.Append($"-cp \"{cp}\" ");
        args.Append($"{mainClass} ");
        args.Append($"--username \"{account.Username}\" ");
        args.Append($"--version {profile.VersionId} ");
        args.Append($"--gameDir \"{_gameDirectory}\" ");
        args.Append($"--assetsDir \"{assetsDir}\" ");
        args.Append($"--assetIndex {assetIndex} ");
        args.Append($"--uuid {account.Uuid ?? Guid.NewGuid().ToString()} ");
        args.Append($"--accessToken {account.AccessToken ?? "0"} ");
        args.Append("--userType " + (account.Type == AccountType.Microsoft ? "msa" : "legacy") + " ");
        args.Append("--versionType release");

        log?.Report($"Запуск: {javaPath}");
        log?.Report($"Версия: {profile.VersionId}, игрок: {account.Username}");
        log?.Report($"Библиотек в classpath: {classpath.Count}");

        var psi = new ProcessStartInfo
        {
            FileName = javaPath,
            Arguments = args.ToString(),
            WorkingDirectory = _gameDirectory,
            UseShellExecute = false,
            RedirectStandardOutput = true,
            RedirectStandardError = true,
            CreateNoWindow = true
        };

        var process = new Process { StartInfo = psi, EnableRaisingEvents = true };

        process.OutputDataReceived += (s, e) =>
        {
            if (!string.IsNullOrEmpty(e.Data)) log?.Report(e.Data);
        };
        process.ErrorDataReceived += (s, e) =>
        {
            if (!string.IsNullOrEmpty(e.Data)) log?.Report("[ERR] " + e.Data);
        };

        process.Start();
        process.BeginOutputReadLine();
        process.BeginErrorReadLine();

        return process;
    }

    private void ExtractNatives(JObject root, string nativesDir, IProgress<string>? log)
    {
        var libraries = root["libraries"] as JArray;
        if (libraries == null) return;

        foreach (var lib in libraries)
        {
            if (!IsLibraryAllowed(lib)) continue;

            // Natives могут быть в downloads.classifiers.natives-windows
            // или как отдельный artifact с natives в имени
            var classifiers = lib["downloads"]?["classifiers"];
            if (classifiers != null)
            {
                // Старый формат (до ~1.19)
                var nativeKeys = new[] { "natives-windows", "natives-windows-64", "natives-windows-arm64" };
                foreach (var key in nativeKeys)
                {
                    var nativeArt = classifiers[key];
                    if (nativeArt == null) continue;

                    var path = nativeArt["path"]?.ToString();
                    if (string.IsNullOrEmpty(path)) continue;

                    var full = Path.Combine(_librariesPath, path.Replace('/', Path.DirectorySeparatorChar));
                    if (File.Exists(full))
                        ExtractZip(full, nativesDir);
                }
            }

            // Новый формат: natives как обычные jars с "-natives-windows" в имени
            var artifact = lib["downloads"]?["artifact"];
            if (artifact != null)
            {
                var path = artifact["path"]?.ToString() ?? "";
                if (path.Contains("natives-windows") && !path.Contains("arm64") && !path.Contains("-x86"))
                {
                    var full = Path.Combine(_librariesPath, path.Replace('/', Path.DirectorySeparatorChar));
                    if (File.Exists(full))
                        ExtractZip(full, nativesDir);
                }
            }
        }
    }

    private static void ExtractZip(string zipPath, string destDir)
    {
        try
        {
            using var archive = ZipFile.OpenRead(zipPath);
            foreach (var entry in archive.Entries)
            {
                if (string.IsNullOrEmpty(entry.Name)) continue; // папки
                if (entry.FullName.StartsWith("META-INF", StringComparison.OrdinalIgnoreCase)) continue;

                var dest = Path.Combine(destDir, entry.Name);
                if (!File.Exists(dest))
                    entry.ExtractToFile(dest, overwrite: true);
            }
        }
        catch
        {
            // игнорируем ошибки распаковки отдельных native-jar
        }
    }

    private static bool IsLibraryAllowed(JToken lib)
    {
        var rules = lib["rules"] as JArray;
        if (rules == null || rules.Count == 0)
            return true;

        bool allowed = false;
        foreach (var rule in rules)
        {
            var action = rule["action"]?.ToString();
            var osName = rule["os"]?["name"]?.ToString();

            if (osName == null)
            {
                allowed = action == "allow";
            }
            else if (osName == "windows")
            {
                allowed = action == "allow";
            }
        }

        return allowed;
    }
}
