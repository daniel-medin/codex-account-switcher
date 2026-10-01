using CodexAccountSwitcher.Models;

namespace CodexAccountSwitcher.Services;

public interface IAccountManagerService
{
    Task<IReadOnlyList<CodexAccount>> GetAccountsAsync(CancellationToken cancellationToken = default);
    Task<CodexAccount> ImportCurrentAccountAsync(string displayName, CancellationToken cancellationToken = default);
    Task<CodexAccount> AddAccountViaLoginAsync(string displayName, CancellationToken cancellationToken = default);
    Task ActivateAccountAsync(Guid accountId, CancellationToken cancellationToken = default);
}
