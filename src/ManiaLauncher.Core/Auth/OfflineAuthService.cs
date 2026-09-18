using ManiaLauncher.Core.Models;
using System.Security.Cryptography;
using System.Text;

namespace ManiaLauncher.Core.Auth;

public class OfflineAuthService
{
    public Account Login(string username)
    {
        if (string.IsNullOrWhiteSpace(username))
            throw new ArgumentException("Username cannot be empty");

        // Generate offline UUID (same algorithm as Minecraft)
        var uuid = GenerateOfflineUuid(username);

        return new Account
        {
            Username = username.Trim(),
            Type = AccountType.Offline,
            Uuid = uuid,
            AccessToken = "0", // offline token
            SkinUrl = null
        };
    }

    private static string GenerateOfflineUuid(string username)
    {
        // Minecraft offline UUID = MD5("OfflinePlayer:" + username)
        var input = "OfflinePlayer:" + username;
        var hash = MD5.HashData(Encoding.UTF8.GetBytes(input));

        // Format as UUID v3
        hash[6] = (byte)((hash[6] & 0x0F) | 0x30);
        hash[8] = (byte)((hash[8] & 0x3F) | 0x80);

        return new Guid(hash).ToString();
    }
}
