using CodexAccountSwitcher.Models;

namespace CodexAccountSwitcher.Services;

public interface IAccountStoreService
{
    Task<IReadOnlyList<CodexAccount>> LoadAccountsAsync(CancellationToken cancellationToken = default);
    Task SaveAccountsAsync(IEnumerable<CodexAccount> accounts, CancellationToken cancellationToken = default);
    Task SaveCredentialsAsync(Guid accountId, byte[] authState, CancellationToken cancellationToken = default);
    Task<byte[]> LoadCredentialsAsync(Guid accountId, CancellationToken cancellationToken = default);
}
