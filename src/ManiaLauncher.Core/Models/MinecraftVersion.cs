using Newtonsoft.Json;

namespace ManiaLauncher.Core.Models;

public class MinecraftVersion
{
    [JsonProperty("id")]
    public string Id { get; set; } = string.Empty;

    [JsonProperty("type")]
    public string Type { get; set; } = "release";

    [JsonProperty("url")]
    public string Url { get; set; } = string.Empty;

    [JsonProperty("time")]
    public DateTime Time { get; set; }

    [JsonProperty("releaseTime")]
    public DateTime ReleaseTime { get; set; }

    [JsonIgnore]
    public bool IsInstalled { get; set; }
}

public class VersionManifest
{
    [JsonProperty("latest")]
    public LatestVersions Latest { get; set; } = new();

    [JsonProperty("versions")]
    public List<MinecraftVersion> Versions { get; set; } = new();
}

public class LatestVersions
{
    [JsonProperty("release")]
    public string Release { get; set; } = string.Empty;

    [JsonProperty("snapshot")]
    public string Snapshot { get; set; } = string.Empty;
}
