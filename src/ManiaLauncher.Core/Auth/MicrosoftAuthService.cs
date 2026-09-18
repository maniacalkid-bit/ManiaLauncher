using ManiaLauncher.Core.Models;
using Newtonsoft.Json;
using Newtonsoft.Json.Linq;
using System.Net.Http.Headers;
using System.Text;

namespace ManiaLauncher.Core.Auth;

/// <summary>
/// Full Microsoft → Xbox Live → XSTS → Minecraft Services authentication.
/// Uses OAuth 2.0 Device Code flow (best for desktop apps).
///
/// SETUP:
/// 1. Go to https://portal.azure.com → Azure Active Directory → App registrations → New registration
/// 2. Name: ManiaLauncher (or any)
/// 3. Supported account types: "Accounts in any organizational directory and personal Microsoft accounts"
///    or "Personal Microsoft accounts only"
/// 4. Redirect URI: leave empty (Device Code doesn't need it)
/// 5. After creation copy "Application (client) ID"
/// 6. Paste the Client ID into the constant below.
/// </summary>
public class MicrosoftAuthService
{
    // ╔════════════════════════════════════════════════════════════╗
    // ║  REPLACE THIS WITH YOUR OWN AZURE APPLICATION CLIENT ID  ║
    // ╚════════════════════════════════════════════════════════════╝
    private const string ClientId = "YOUR_AZURE_CLIENT_ID_HERE";

    private const string DeviceCodeEndpoint = "https://login.microsoftonline.com/consumers/oauth2/v2.0/devicecode";
    private const string TokenEndpoint = "https://login.microsoftonline.com/consumers/oauth2/v2.0/token";
    private const string XboxAuthEndpoint = "https://user.auth.xboxlive.com/user/authenticate";
    private const string XstsEndpoint = "https://xsts.auth.xboxlive.com/xsts/authorize";
    private const string MinecraftLoginEndpoint = "https://api.minecraftservices.com/authentication/login_with_xbox";
    private const string MinecraftProfileEndpoint = "https://api.minecraftservices.com/minecraft/profile";

    private readonly HttpClient _http;

    public MicrosoftAuthService()
    {
        _http = new HttpClient();
        _http.DefaultRequestHeaders.Add("User-Agent", "ManiaLauncher/1.0");
        _http.DefaultRequestHeaders.Accept.Add(new MediaTypeWithQualityHeaderValue("application/json"));
    }

    public async Task<Account> LoginAsync(IProgress<string>? progress = null, CancellationToken ct = default)
    {
        if (string.IsNullOrWhiteSpace(ClientId) || ClientId == "YOUR_AZURE_CLIENT_ID_HERE")
        {
            throw new InvalidOperationException(
                "Microsoft Auth is not configured.\n\n" +
                "Open src/ManiaLauncher.Core/Auth/MicrosoftAuthService.cs\n" +
                "and replace YOUR_AZURE_CLIENT_ID_HERE with your Azure Application (client) ID.\n\n" +
                "See README.md for detailed instructions.");
        }

        // ─── 1. Device Code ───────────────────────────────────────
        progress?.Report("Requesting device code from Microsoft...");
        var device = await RequestDeviceCodeAsync(ct);

        progress?.Report($"Open {device.VerificationUri} and enter code: {device.UserCode}");

        // ─── 2. Poll for Microsoft Access Token ───────────────────
        progress?.Report("Waiting for you to authorize in browser...");
        var msToken = await PollForMicrosoftTokenAsync(device, progress, ct);

        // ─── 3. Xbox Live Authenticate ────────────────────────────
        progress?.Report("Authenticating with Xbox Live...");
        var (xblToken, userHash) = await AuthenticateXboxLiveAsync(msToken.AccessToken, ct);

        // ─── 4. XSTS Token ────────────────────────────────────────
        progress?.Report("Getting XSTS token...");
        var xstsToken = await GetXstsTokenAsync(xblToken, ct);

        // ─── 5. Minecraft Services Login ──────────────────────────
        progress?.Report("Logging into Minecraft Services...");
        var mcAccessToken = await LoginToMinecraftAsync(userHash, xstsToken, ct);

        // ─── 6. Minecraft Profile ─────────────────────────────────
        progress?.Report("Fetching Minecraft profile...");
        var (uuid, username, skinUrl) = await GetMinecraftProfileAsync(mcAccessToken, ct);

        progress?.Report($"Success! Logged in as {username}");

        return new Account
        {
            Username = username,
            Type = AccountType.Microsoft,
            AccessToken = mcAccessToken,
            RefreshToken = msToken.RefreshToken,
            Uuid = uuid,
            TokenExpiry = DateTime.UtcNow.AddHours(24),
            SkinUrl = skinUrl
        };
    }

    public async Task<Account?> RefreshAsync(Account account, CancellationToken ct = default)
    {
        if (string.IsNullOrEmpty(account.RefreshToken) || ClientId == "YOUR_AZURE_CLIENT_ID_HERE")
            return null;

        try
        {
            var content = new FormUrlEncodedContent(new Dictionary<string, string>
            {
                ["client_id"] = ClientId,
                ["grant_type"] = "refresh_token",
                ["refresh_token"] = account.RefreshToken,
                ["scope"] = "XboxLive.signin offline_access"
            });

            var response = await _http.PostAsync(TokenEndpoint, content, ct);
            if (!response.IsSuccessStatusCode) return null;

            var json = await response.Content.ReadAsStringAsync(ct);
            var token = JsonConvert.DeserializeObject<MsTokenResponse>(json);
            if (token == null) return null;

            var (xblToken, userHash) = await AuthenticateXboxLiveAsync(token.AccessToken, ct);
            var xstsToken = await GetXstsTokenAsync(xblToken, ct);
            var mcAccessToken = await LoginToMinecraftAsync(userHash, xstsToken, ct);
            var (uuid, username, skinUrl) = await GetMinecraftProfileAsync(mcAccessToken, ct);

            account.AccessToken = mcAccessToken;
            account.RefreshToken = token.RefreshToken ?? account.RefreshToken;
            account.Uuid = uuid;
            account.Username = username;
            account.SkinUrl = skinUrl;
            account.TokenExpiry = DateTime.UtcNow.AddHours(24);
            return account;
        }
        catch
        {
            return null;
        }
    }

    private async Task<DeviceCodeResponse> RequestDeviceCodeAsync(CancellationToken ct)
    {
        var content = new FormUrlEncodedContent(new Dictionary<string, string>
        {
            ["client_id"] = ClientId,
            ["scope"] = "XboxLive.signin offline_access"
        });

        var response = await _http.PostAsync(DeviceCodeEndpoint, content, ct);
        var json = await response.Content.ReadAsStringAsync(ct);
        response.EnsureSuccessStatusCode();
        return JsonConvert.DeserializeObject<DeviceCodeResponse>(json)
               ?? throw new Exception("Failed to parse device code response");
    }

    private async Task<MsTokenResponse> PollForMicrosoftTokenAsync(
        DeviceCodeResponse device, IProgress<string>? progress, CancellationToken ct)
    {
        var interval = TimeSpan.FromSeconds(Math.Max(device.Interval, 5));
        var expiresAt = DateTime.UtcNow.AddSeconds(device.ExpiresIn);

        while (DateTime.UtcNow < expiresAt)
        {
            await Task.Delay(interval, ct);

            var content = new FormUrlEncodedContent(new Dictionary<string, string>
            {
                ["grant_type"] = "urn:ietf:params:oauth:grant-type:device_code",
                ["client_id"] = ClientId,
                ["device_code"] = device.DeviceCode
            });

            var response = await _http.PostAsync(TokenEndpoint, content, ct);
            var json = await response.Content.ReadAsStringAsync(ct);

            if (response.IsSuccessStatusCode)
            {
                return JsonConvert.DeserializeObject<MsTokenResponse>(json)
                       ?? throw new Exception("Failed to parse token response");
            }

            var error = JObject.Parse(json);
            var errorCode = error["error"]?.ToString();

            if (errorCode == "authorization_pending")
            {
                progress?.Report("Still waiting for authorization...");
                continue;
            }

            if (errorCode == "slow_down")
            {
                interval += TimeSpan.FromSeconds(5);
                continue;
            }

            if (errorCode == "expired_token")
                throw new TimeoutException("Device code expired. Please try again.");

            throw new Exception($"Microsoft auth error: {error["error_description"] ?? errorCode}");
        }

        throw new TimeoutException("Device code expired. Please try again.");
    }

    private async Task<(string Token, string UserHash)> AuthenticateXboxLiveAsync(string msAccessToken, CancellationToken ct)
    {
        var body = new
        {
            Properties = new
            {
                AuthMethod = "RPS",
                SiteName = "user.auth.xboxlive.com",
                RpsTicket = "d=" + msAccessToken
            },
            RelyingParty = "http://auth.xboxlive.com",
            TokenType = "JWT"
        };

        var json = JsonConvert.SerializeObject(body);
        using var content = new StringContent(json, Encoding.UTF8, "application/json");
        using var request = new HttpRequestMessage(HttpMethod.Post, XboxAuthEndpoint);
        request.Content = content;
        request.Headers.Add("x-xbl-contract-version", "1");

        var response = await _http.SendAsync(request, ct);
        var responseJson = await response.Content.ReadAsStringAsync(ct);
        response.EnsureSuccessStatusCode();

        var obj = JObject.Parse(responseJson);
        var token = obj["Token"]?.ToString()
                    ?? throw new Exception("No Xbox Live token in response");
        var userHash = obj["DisplayClaims"]?["xui"]?[0]?["uhs"]?.ToString()
                       ?? throw new Exception("No user hash in Xbox Live response");

        return (token, userHash);
    }

    private async Task<string> GetXstsTokenAsync(string xblToken, CancellationToken ct)
    {
        var body = new
        {
            Properties = new
            {
                SandboxId = "RETAIL",
                UserTokens = new[] { xblToken }
            },
            RelyingParty = "rp://api.minecraftservices.com/",
            TokenType = "JWT"
        };

        var json = JsonConvert.SerializeObject(body);
        using var content = new StringContent(json, Encoding.UTF8, "application/json");
        using var request = new HttpRequestMessage(HttpMethod.Post, XstsEndpoint);
        request.Content = content;
        request.Headers.Add("x-xbl-contract-version", "1");

        var response = await _http.SendAsync(request, ct);
        var responseJson = await response.Content.ReadAsStringAsync(ct);

        if (!response.IsSuccessStatusCode)
        {
            var err = JObject.Parse(responseJson);
            var xerr = err["XErr"]?.ToString();
            throw new Exception($"XSTS error {xerr}: {err["Message"] ?? responseJson}");
        }

        var obj = JObject.Parse(responseJson);
        return obj["Token"]?.ToString()
               ?? throw new Exception("No XSTS token in response");
    }

    private async Task<string> LoginToMinecraftAsync(string userHash, string xstsToken, CancellationToken ct)
    {
        var body = new
        {
            identityToken = $"XBL3.0 x={userHash};{xstsToken}"
        };

        var json = JsonConvert.SerializeObject(body);
        using var content = new StringContent(json, Encoding.UTF8, "application/json");

        var response = await _http.PostAsync(MinecraftLoginEndpoint, content, ct);
        var responseJson = await response.Content.ReadAsStringAsync(ct);
        response.EnsureSuccessStatusCode();

        var obj = JObject.Parse(responseJson);
        return obj["access_token"]?.ToString()
               ?? throw new Exception("No Minecraft access token");
    }

    private async Task<(string Uuid, string Username, string? SkinUrl)> GetMinecraftProfileAsync(
        string mcAccessToken, CancellationToken ct)
    {
        using var request = new HttpRequestMessage(HttpMethod.Get, MinecraftProfileEndpoint);
        request.Headers.Authorization = new AuthenticationHeaderValue("Bearer", mcAccessToken);

        var response = await _http.SendAsync(request, ct);
        var responseJson = await response.Content.ReadAsStringAsync(ct);

        if (!response.IsSuccessStatusCode)
        {
            throw new Exception(
                "This Microsoft account does not own Minecraft Java Edition.\n" +
                "Buy the game at https://www.minecraft.net/");
        }

        var obj = JObject.Parse(responseJson);
        var uuid = obj["id"]?.ToString()
                   ?? throw new Exception("No UUID in profile");
        var username = obj["name"]?.ToString()
                       ?? throw new Exception("No username in profile");

        string? skinUrl = null;
        var skins = obj["skins"] as JArray;
        if (skins != null && skins.Count > 0)
        {
            skinUrl = skins[0]?["url"]?.ToString();
        }

        if (uuid.Length == 32)
        {
            uuid = $"{uuid[..8]}-{uuid[8..12]}-{uuid[12..16]}-{uuid[16..20]}-{uuid[20..]}";
        }

        return (uuid, username, skinUrl);
    }

    private class DeviceCodeResponse
    {
        [JsonProperty("device_code")] public string DeviceCode { get; set; } = "";
        [JsonProperty("user_code")] public string UserCode { get; set; } = "";
        [JsonProperty("verification_uri")] public string VerificationUri { get; set; } = "";
        [JsonProperty("expires_in")] public int ExpiresIn { get; set; }
        [JsonProperty("interval")] public int Interval { get; set; } = 5;
    }

    private class MsTokenResponse
    {
        [JsonProperty("access_token")] public string AccessToken { get; set; } = "";
        [JsonProperty("refresh_token")] public string? RefreshToken { get; set; }
        [JsonProperty("expires_in")] public int ExpiresIn { get; set; }
        [JsonProperty("token_type")] public string TokenType { get; set; } = "";
    }
}
