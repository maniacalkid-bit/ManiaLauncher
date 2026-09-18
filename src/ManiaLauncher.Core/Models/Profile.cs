namespace ManiaLauncher.Core.Models;

public class Profile
{
    public string Id { get; set; } = Guid.NewGuid().ToString();
    public string Name { get; set; } = "New Profile";
    public string VersionId { get; set; } = "1.21.1"; // default version
    public string AccountId { get; set; } = string.Empty;
    public string JavaPath { get; set; } = string.Empty;
    public string JvmArguments { get; set; } = "-Xmx2G -Xms1G";
    public string GameDirectory { get; set; } = string.Empty;
    public DateTime CreatedAt { get; set; } = DateTime.UtcNow;
    public DateTime LastUsed { get; set; } = DateTime.UtcNow;
}
