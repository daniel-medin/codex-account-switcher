using CodexAccountSwitcher.Models;

namespace CodexAccountSwitcher.Services;

public interface ICodexWorkerRestartService
{
    IReadOnlyList<CodexWorkerTarget> FindTargets();
    void ValidateReady(CodexWorkerTarget target);
    Task RestartAsync(CodexWorkerTarget target, CancellationToken cancellationToken = default);
}
