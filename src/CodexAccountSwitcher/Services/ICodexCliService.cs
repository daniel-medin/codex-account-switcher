namespace CodexAccountSwitcher.Services;

public interface ICodexCliService
{
    Task<string?> FindCodexExecutableAsync(CancellationToken cancellationToken = default);

    Task<int> RunInteractiveLoginAsync(
        string executablePath,
        string codexHome,
        CancellationToken cancellationToken = default);
}
