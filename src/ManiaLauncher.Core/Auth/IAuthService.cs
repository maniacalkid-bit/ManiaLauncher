using ManiaLauncher.Core.Models;

namespace ManiaLauncher.Core.Auth;

public interface IAuthService
{
    Task<Account> LoginOfflineAsync(string username);
    Task<Account> LoginMicrosoftAsync(CancellationToken cancellationToken = default);
    Task<Account?> RefreshMicrosoftTokenAsync(Account account);
    Task LogoutAsync(Account account);
}
