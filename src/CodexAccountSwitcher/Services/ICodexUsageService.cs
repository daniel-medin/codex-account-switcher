using CodexAccountSwitcher.Models;

namespace CodexAccountSwitcher.Services;

public interface ICodexUsageService
{
    Task<CodexUsage> GetUsageAsync(
        CodexAccount account,
        CancellationToken cancellationToken = default);
}
