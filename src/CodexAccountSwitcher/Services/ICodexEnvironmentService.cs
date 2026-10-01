using CodexAccountSwitcher.Models;

namespace CodexAccountSwitcher.Services;

public interface ICodexEnvironmentService
{
    Task<CodexDiagnostics> GetDiagnosticsAsync(CancellationToken cancellationToken = default);
}
