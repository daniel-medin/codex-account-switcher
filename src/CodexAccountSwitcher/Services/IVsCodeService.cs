namespace CodexAccountSwitcher.Services;

public interface IVsCodeService
{
    Task<int> ReloadCodexProcessesAsync(CancellationToken cancellationToken = default);
}
