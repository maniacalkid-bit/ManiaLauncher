namespace ManiaLauncher.Core.Models;

public enum AccountType
{
    Offline,
    Microsoft
}

public class Account
{
    public string Id { get; set; } = Guid.NewGuid().ToString();
    public string Username { get; set; } = string.Empty;
    public AccountType Type { get; set; }
    public string? AccessToken { get; set; }
    public string? RefreshToken { get; set; }
    public string? Uuid { get; set; }
    public DateTime? TokenExpiry { get; set; }
    public string? SkinUrl { get; set; }

    public bool IsTokenValid =>
        Type == AccountType.Offline ||
        (AccessToken != null && TokenExpiry.HasValue && TokenExpiry > DateTime.UtcNow);
}
