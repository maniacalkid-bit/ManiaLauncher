using System.IO;
using System.Net.Http;
using System.Security.Cryptography;
using System.Text;
using System.Text.Json;
using System.Text.Json.Serialization;
using System.ComponentModel;
using System.Runtime.CompilerServices;

namespace ManiaLauncher.Services;

/// <summary>
/// A single promotional banner described by the remote JSON document.
/// See README.md for the full schema.
/// </summary>
public sealed class AdItem
{
    public string? Id { get; set; }
    public bool Enabled { get; set; } = true;
    public string? Title { get; set; }
    public string Text { get; set; } = "";
    public string? ImageUrl { get; set; }
    public string? LinkUrl { get; set; }
    public string? BackgroundColor { get; set; }
    public string? TextColor { get; set; }
    public bool Dismissible { get; set; } = true;

    [JsonIgnore]
    public bool HasLink => !string.IsNullOrWhiteSpace(LinkUrl);

    /// <summary>Stable fingerprint used to remember a dismissal.</summary>
    [JsonIgnore]
    public string Fingerprint => !string.IsNullOrWhiteSpace(Id) ? Id! : ComputeFingerprint();

    private string ComputeFingerprint()
    {
        var raw = $"{Text}|{LinkUrl}|{ImageUrl}";
        var hash = SHA1.HashData(Encoding.UTF8.GetBytes(raw));
        return Convert.ToHexString(hash)[..16].ToLowerInvariant();
    }
}

/// <summary>
/// Loads the promotional banner shown on the Home page from a remote raw JSON file
/// (the URL is <see cref="AppInfo.AdsUrl"/>).
///
/// Behavior:
///  - the last successfully downloaded banner is cached locally and shown when offline;
///  - a banner the user dismissed (by id or content fingerprint) stays hidden;
///  - any failure is logged and leaves the previous/cached banner in place —
///    the launcher never blocks on ads.
/// </summary>
public sealed class AdService : INotifyPropertyChanged
{
    private static readonly Lazy<AdService> Lazy = new(() => new AdService());
    public static AdService Instance => Lazy.Value;

    private static readonly JsonSerializerOptions JsonOptions = new()
    {
        PropertyNameCaseInsensitive = true,
        WriteIndented = true,
        DefaultIgnoreCondition = JsonIgnoreCondition.WhenWritingNull
    };

    private static string CachePath => Path.Combine(AppInfo.AppDataDir, "ads-cache.json");

    private AdService() { }

    public event PropertyChangedEventHandler? PropertyChanged;
    private void OnChanged([CallerMemberName] string? n = null) =>
        PropertyChanged?.Invoke(this, new PropertyChangedEventArgs(n));

    private AdItem? _current;
    /// <summary>Banner to display, or null when nothing should be shown.</summary>
    public AdItem? Current
    {
        get => _current;
        private set { _current = value; OnChanged(); }
    }

    private string _source = "none";
    /// <summary>Where the active banner came from: "remote", "cache" or "none".</summary>
    public string Source
    {
        get => _source;
        private set { _source = value; OnChanged(); }
    }

    /// <summary>
    /// Shows the cached banner immediately, then refreshes from the network.
    /// </summary>
    public async Task LoadAsync(CancellationToken ct = default)
    {
        // 1) Cached banner right away so the UI is never empty on slow links.
        var cached = ReadCache();
        if (cached != null) Apply(cached, "cache");

        // 2) Remote document.
        try
        {
            using var http = new HttpClient(new HttpClientHandler
            {
                AutomaticDecompression = System.Net.DecompressionMethods.All
            })
            {
                Timeout = TimeSpan.FromSeconds(15)
            };
            http.DefaultRequestHeaders.UserAgent.ParseAdd($"{AppInfo.Brand}/{AppInfo.Version}");
            http.DefaultRequestHeaders.CacheControl =
                new System.Net.Http.Headers.CacheControlHeaderValue { NoCache = true };

            var json = await http.GetStringAsync(AppInfo.AdsUrl, ct).ConfigureAwait(true);
            var remote = JsonSerializer.Deserialize<AdItem>(json, JsonOptions);
            if (remote == null)
            {
                LogService.Instance.Warn("Ads: remote JSON parsed to null");
                return;
            }

            WriteCache(remote);
            Apply(remote, "remote");
            LogService.Instance.Info($"Ads: banner loaded from network (id={remote.Fingerprint})");
        }
        catch (Exception ex)
        {
            LogService.Instance.Warn($"Ads: remote fetch failed ({ex.GetType().Name}: {ex.Message}). " +
                                     (Current != null ? "Using cached banner." : "No banner available."));
        }
    }

    private void Apply(AdItem item, string source)
    {
        if (!item.Enabled || string.IsNullOrWhiteSpace(item.Text) ||
            SettingsService.Instance.IsAdDismissed(item.Fingerprint))
        {
            Current = null;
            Source = "none";
            return;
        }

        Current = item;
        Source = source;
    }

    /// <summary>Hides the banner and remembers the dismissal across launches.</summary>
    public void Dismiss()
    {
        var item = Current;
        if (item == null) return;

        SettingsService.Instance.AddDismissedAd(item.Fingerprint);
        Current = null;
        Source = "none";
        LogService.Instance.Info($"Ads: banner dismissed (id={item.Fingerprint})");
    }

    /// <summary>Opens the banner link in the default browser (http/https only).</summary>
    public void OpenLink()
    {
        var url = Current?.LinkUrl;
        if (string.IsNullOrWhiteSpace(url)) return;

        // The document is remote: never let it launch anything but a web link.
        if (!Uri.TryCreate(url, UriKind.Absolute, out var uri) ||
            (uri.Scheme != Uri.UriSchemeHttp && uri.Scheme != Uri.UriSchemeHttps))
        {
            LogService.Instance.Warn($"Ads: refused to open non-http link '{url}'");
            return;
        }

        try
        {
            System.Diagnostics.Process.Start(
                new System.Diagnostics.ProcessStartInfo(uri.AbsoluteUri) { UseShellExecute = true });
            LogService.Instance.Info($"Ads: opened link {uri.AbsoluteUri}");
        }
        catch (Exception ex)
        {
            LogService.Instance.Error($"Ads: failed to open link: {ex.Message}");
        }
    }

    // ---------- local cache ----------

    private static AdItem? ReadCache()
    {
        try
        {
            if (!File.Exists(CachePath)) return null;
            return JsonSerializer.Deserialize<AdItem>(File.ReadAllText(CachePath), JsonOptions);
        }
        catch (Exception ex)
        {
            LogService.Instance.Warn($"Ads: failed to read cache: {ex.Message}");
            return null;
        }
    }

    private static void WriteCache(AdItem item)
    {
        try
        {
            Directory.CreateDirectory(AppInfo.AppDataDir);
            File.WriteAllText(CachePath, JsonSerializer.Serialize(item, JsonOptions));
        }
        catch (Exception ex)
        {
            LogService.Instance.Warn($"Ads: failed to write cache: {ex.Message}");
        }
    }
}
