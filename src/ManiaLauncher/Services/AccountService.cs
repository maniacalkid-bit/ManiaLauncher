using System.Collections.ObjectModel;
using System.IO;
using System.Text.Json;
using System.Text.Json.Serialization;
using System.Security.Cryptography;
using System.Text;

namespace ManiaLauncher.Services;

/// <summary>
/// An offline (local) player account used for testing the launcher.
/// The UUID is deterministically derived from the username (offline-mode style).
/// </summary>
public sealed class OfflineAccount
{
    public string Username { get; set; } = "";

    [JsonIgnore]
    public Guid Uuid => OfflineUuid(Username);

    [JsonIgnore]
    public string UuidNoDashes => Uuid.ToString("N");

    /// <summary>
    /// Deterministic offline UUID from a username — the classic offline-mode scheme:
    /// MD5("OfflinePlayer:&lt;name&gt;") formatted as a version-3 UUID.
    /// </summary>
    public static Guid OfflineUuid(string username)
    {
        var bytes = MD5.HashData(Encoding.UTF8.GetBytes("OfflinePlayer:" + username));
        bytes[6] = (byte)((bytes[6] & 0x0f) | 0x30); // version 3
        bytes[8] = (byte)((bytes[8] & 0x3f) | 0x80); // variant
        if (BitConverter.IsLittleEndian)
        {
            Array.Reverse(bytes, 0, 4);
            Array.Reverse(bytes, 4, 2);
            Array.Reverse(bytes, 6, 2);
        }
        return new Guid(bytes);
    }

    public static bool IsValidUsername(string? name) =>
        !string.IsNullOrWhiteSpace(name) &&
        name.Length is >= 2 and <= 16 &&
        name.All(c => char.IsAsciiLetterOrDigit(c) || c == '_');
}

/// <summary>
/// Persists offline accounts to %APPDATA%\ManiaLauncher\accounts.json.
/// </summary>
public sealed class AccountService
{
    private static readonly Lazy<AccountService> Lazy = new(() => new AccountService());
    public static AccountService Instance => Lazy.Value;

    private static string FilePath => Path.Combine(AppInfo.AppDataDir, "accounts.json");
    private static readonly JsonSerializerOptions JsonOptions = new() { WriteIndented = true };

    public ObservableCollection<OfflineAccount> Accounts { get; } = new();
    public OfflineAccount? SelectedAccount { get; private set; }

    private AccountService()
    {
        Load();
    }

    public bool Add(string username)
    {
        username = username.Trim();
        if (!OfflineAccount.IsValidUsername(username)) return false;
        if (Accounts.Any(a => string.Equals(a.Username, username, StringComparison.OrdinalIgnoreCase)))
        {
            Select(Accounts.First(a => string.Equals(a.Username, username, StringComparison.OrdinalIgnoreCase)));
            return true;
        }

        var acc = new OfflineAccount { Username = username };
        Accounts.Add(acc);
        Select(acc);
        Save();
        LogService.Instance.Info($"Added offline account '{username}' (uuid={acc.UuidNoDashes})");
        return true;
    }

    public void Remove(OfflineAccount account)
    {
        if (Accounts.Remove(account))
        {
            if (ReferenceEquals(SelectedAccount, account))
                Select(Accounts.FirstOrDefault());
            Save();
        }
    }

    public void Select(OfflineAccount? account)
    {
        SelectedAccount = account;
        SettingsService.Instance.LastAccount = account?.Username;
    }

    public void Load()
    {
        Accounts.Clear();
        try
        {
            if (File.Exists(FilePath))
            {
                var list = JsonSerializer.Deserialize<List<OfflineAccount>>(File.ReadAllText(FilePath), JsonOptions);
                if (list != null)
                    foreach (var a in list.Where(a => OfflineAccount.IsValidUsername(a.Username)))
                        Accounts.Add(a);
            }
        }
        catch (Exception ex)
        {
            LogService.Instance.Error($"Failed to load accounts: {ex.Message}");
        }

        // Seed a default test account on first run so the launcher is testable immediately.
        if (Accounts.Count == 0)
        {
            Accounts.Add(new OfflineAccount { Username = "Tester" });
            Save();
        }

        var last = SettingsService.Instance.LastAccount;
        Select(last != null
            ? Accounts.FirstOrDefault(a => a.Username.Equals(last, StringComparison.OrdinalIgnoreCase))
            : Accounts.FirstOrDefault());
    }

    private void Save()
    {
        try
        {
            Directory.CreateDirectory(AppInfo.AppDataDir);
            File.WriteAllText(FilePath, JsonSerializer.Serialize(Accounts.ToList(), JsonOptions));
        }
        catch (Exception ex)
        {
            LogService.Instance.Error($"Failed to save accounts: {ex.Message}");
        }
    }
}
