using ManiaLauncher.Core.Models;
using Newtonsoft.Json;
using Newtonsoft.Json.Linq;

namespace ManiaLauncher.Core.Services;

public class VersionService
{
    private const string ManifestUrl = "https://launchermeta.mojang.com/mc/game/version_manifest_v2.json";
    private const string ResourcesUrl = "https://resources.download.minecraft.net";

    private readonly HttpClient _http;
    private readonly string _gameDir;
    private readonly string _versionsPath;
    private readonly string _librariesPath;
    private readonly string _assetsPath;

    public VersionService(string gameDirectory)
    {
        _gameDir = gameDirectory;
        _versionsPath = Path.Combine(gameDirectory, "versions");
        _librariesPath = Path.Combine(gameDirectory, "libraries");
        _assetsPath = Path.Combine(gameDirectory, "assets");
        Directory.CreateDirectory(_versionsPath);
        Directory.CreateDirectory(_librariesPath);
        Directory.CreateDirectory(_assetsPath);

        _http = new HttpClient();
        _http.DefaultRequestHeaders.UserAgent.ParseAdd("ManiaLauncher/1.0");
        _http.Timeout = TimeSpan.FromMinutes(15);
    }

    public async Task<VersionManifest> GetVersionManifestAsync(CancellationToken ct = default)
    {
        var json = await _http.GetStringAsync(ManifestUrl, ct);
        var manifest = JsonConvert.DeserializeObject<VersionManifest>(json)
                       ?? throw new Exception("Не удалось разобрать список версий");

        foreach (var v in manifest.Versions)
        {
            var dir = Path.Combine(_versionsPath, v.Id);
            v.IsInstalled = Directory.Exists(dir)
                            && File.Exists(Path.Combine(dir, $"{v.Id}.json"))
                            && File.Exists(Path.Combine(dir, $"{v.Id}.jar"));
        }

        return manifest;
    }

    public async Task DownloadVersionAsync(
        MinecraftVersion version,
        IProgress<string>? log = null,
        CancellationToken ct = default)
    {
        if (string.IsNullOrEmpty(version.Url))
            throw new Exception("У версии нет URL для скачивания");

        var versionDir = Path.Combine(_versionsPath, version.Id);
        Directory.CreateDirectory(versionDir);

        // 1. Version JSON
        log?.Report($"Скачивание информации о версии {version.Id}...");
        var versionJson = await _http.GetStringAsync(version.Url, ct);
        var jsonPath = Path.Combine(versionDir, $"{version.Id}.json");
        await File.WriteAllTextAsync(jsonPath, versionJson, ct);

        var root = JObject.Parse(versionJson);

        // 2. client.jar
        var clientUrl = root["downloads"]?["client"]?["url"]?.ToString();
        if (string.IsNullOrEmpty(clientUrl))
            throw new Exception("В JSON версии нет ссылки на client.jar");

        var jarPath = Path.Combine(versionDir, $"{version.Id}.jar");
        if (!File.Exists(jarPath))
        {
            log?.Report("Скачивание client.jar...");
            await DownloadFileAsync(clientUrl, jarPath, ct);
        }
        else
        {
            log?.Report("client.jar уже скачан");
        }

        // 3. Libraries
        var libraries = root["libraries"] as JArray;
        if (libraries != null)
        {
            int total = libraries.Count;
            int done = 0;
            int downloaded = 0;

            foreach (var lib in libraries)
            {
                ct.ThrowIfCancellationRequested();
                done++;

                if (!IsLibraryAllowed(lib))
                    continue;

                // artifact
                var artifact = lib["downloads"]?["artifact"];
                if (artifact != null)
                {
                    var libUrl = artifact["url"]?.ToString();
                    var libPath = artifact["path"]?.ToString();
                    if (!string.IsNullOrEmpty(libUrl) && !string.IsNullOrEmpty(libPath))
                    {
                        var fullPath = Path.Combine(_librariesPath, libPath.Replace('/', Path.DirectorySeparatorChar));
                        if (!File.Exists(fullPath))
                        {
                            Directory.CreateDirectory(Path.GetDirectoryName(fullPath)!);
                            log?.Report($"Библиотека ({done}/{total}): {Path.GetFileName(libPath)}");
                            await DownloadFileAsync(libUrl, fullPath, ct);
                            downloaded++;
                        }
                    }
                }

                // classifiers (natives для старых версий)
                var classifiers = lib["downloads"]?["classifiers"] as JObject;
                if (classifiers != null)
                {
                    foreach (var prop in classifiers.Properties())
                    {
                        if (!prop.Name.Contains("natives-windows")) continue;
                        if (prop.Name.Contains("arm64") || prop.Name.Contains("x86")) continue;

                        var nUrl = prop.Value["url"]?.ToString();
                        var nPath = prop.Value["path"]?.ToString();
                        if (string.IsNullOrEmpty(nUrl) || string.IsNullOrEmpty(nPath)) continue;

                        var fullPath = Path.Combine(_librariesPath, nPath.Replace('/', Path.DirectorySeparatorChar));
                        if (!File.Exists(fullPath))
                        {
                            Directory.CreateDirectory(Path.GetDirectoryName(fullPath)!);
                            log?.Report($"Natives: {Path.GetFileName(nPath)}");
                            await DownloadFileAsync(nUrl, fullPath, ct);
                            downloaded++;
                        }
                    }
                }
            }

            log?.Report($"Библиотеки готовы (скачано новых: {downloaded})");
        }

        // 4. Assets
        await DownloadAssetsAsync(root, log, ct);

        log?.Report($"Версия {version.Id} успешно установлена!");
    }

    private async Task DownloadAssetsAsync(JObject versionRoot, IProgress<string>? log, CancellationToken ct)
    {
        var assetIndexId = versionRoot["assetIndex"]?["id"]?.ToString();
        var assetIndexUrl = versionRoot["assetIndex"]?["url"]?.ToString();

        if (string.IsNullOrEmpty(assetIndexId) || string.IsNullOrEmpty(assetIndexUrl))
        {
            log?.Report("Asset index не найден, пропускаем assets");
            return;
        }

        var indexesDir = Path.Combine(_assetsPath, "indexes");
        var objectsDir = Path.Combine(_assetsPath, "objects");
        Directory.CreateDirectory(indexesDir);
        Directory.CreateDirectory(objectsDir);

        var indexPath = Path.Combine(indexesDir, $"{assetIndexId}.json");
        if (!File.Exists(indexPath))
        {
            log?.Report($"Скачивание asset index ({assetIndexId})...");
            await DownloadFileAsync(assetIndexUrl, indexPath, ct);
        }

        var indexJson = await File.ReadAllTextAsync(indexPath, ct);
        var index = JObject.Parse(indexJson);
        var objects = index["objects"] as JObject;

        if (objects == null || objects.Count == 0)
        {
            log?.Report("Asset index пуст");
            return;
        }

        // Собираем список того, что нужно скачать
        var toDownload = new List<(string Hash, string Path, long Size)>();
        foreach (var prop in objects.Properties())
        {
            var hash = prop.Value["hash"]?.ToString();
            if (string.IsNullOrEmpty(hash) || hash.Length < 2) continue;

            var size = prop.Value["size"]?.Value<long>() ?? 0;
            var objPath = Path.Combine(objectsDir, hash[..2], hash);
            if (!File.Exists(objPath))
                toDownload.Add((hash, objPath, size));
        }

        if (toDownload.Count == 0)
        {
            log?.Report("Все assets уже скачаны");
            return;
        }

        log?.Report($"Скачивание assets: {toDownload.Count} файлов...");

        int completed = 0;
        int total = toDownload.Count;
        var semaphore = new SemaphoreSlim(8); // параллельно 8 загрузок
        var tasks = new List<Task>();

        foreach (var (hash, path, size) in toDownload)
        {
            await semaphore.WaitAsync(ct);
            tasks.Add(Task.Run(async () =>
            {
                try
                {
                    Directory.CreateDirectory(Path.GetDirectoryName(path)!);
                    var url = $"{ResourcesUrl}/{hash[..2]}/{hash}";
                    await DownloadFileAsync(url, path, ct);

                    var done = Interlocked.Increment(ref completed);
                    if (done % 50 == 0 || done == total)
                        log?.Report($"Assets: {done}/{total}");
                }
                catch (Exception ex)
                {
                    log?.Report($"Ошибка asset {hash[..8]}...: {ex.Message}");
                }
                finally
                {
                    semaphore.Release();
                }
            }, ct));
        }

        await Task.WhenAll(tasks);
        log?.Report($"Assets скачаны ({total} файлов)");
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
                allowed = action == "allow";
            else if (osName == "windows")
                allowed = action == "allow";
        }

        return allowed;
    }

    private async Task DownloadFileAsync(string url, string path, CancellationToken ct)
    {
        using var response = await _http.GetAsync(url, HttpCompletionOption.ResponseHeadersRead, ct);
        response.EnsureSuccessStatusCode();

        await using var fs = new FileStream(path, FileMode.Create, FileAccess.Write, FileShare.None);
        await response.Content.CopyToAsync(fs, ct);
    }
}
