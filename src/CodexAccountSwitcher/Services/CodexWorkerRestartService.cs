using System.Diagnostics;
using System.Text.RegularExpressions;
using CodexAccountSwitcher.Models;

namespace CodexAccountSwitcher.Services;

public sealed class CodexWorkerRestartService : ICodexWorkerRestartService
{
    private readonly IWindowsProcessInventory _inventory;
    private readonly ICodexRecoveryUi _recoveryUi;
    private readonly int _sessionId;
    private readonly string? _requiredUserDataDirectory;
    private readonly bool _profileConfigured;

    public CodexWorkerRestartService()
        : this(
            new WindowsProcessInventory(),
            new CodexRecoveryUi(),
            Process.GetCurrentProcess().SessionId,
            ResolveUserDataDirectory())
    {
    }

    public CodexWorkerRestartService(
        IWindowsProcessInventory inventory,
        ICodexRecoveryUi recoveryUi,
        int sessionId,
        string? requiredUserDataDirectory = null)
    {
        _inventory = inventory;
        _recoveryUi = recoveryUi;
        _sessionId = sessionId;
        _requiredUserDataDirectory = requiredUserDataDirectory is null
            ? null
            : requiredUserDataDirectory.Length == 0
                ? string.Empty
                : Path.GetFullPath(requiredUserDataDirectory).TrimEnd(Path.DirectorySeparatorChar);
        _profileConfigured = requiredUserDataDirectory is not null;
    }

    public IReadOnlyList<CodexWorkerTarget> FindTargets()
    {
        if (_profileConfigured && _requiredUserDataDirectory?.Length == 0)
        {
            throw new InvalidOperationException(
                "Set CODEX_ACCOUNT_SWITCHER_VSCODE_USER_DATA_DIR to the matching VS Code profile when using a custom CODEX_HOME.");
        }

        var processes = _inventory.Read().ToDictionary(process => process.Id);
        var targets = new List<CodexWorkerTarget>();

        foreach (var worker in processes.Values)
        {
            if (worker.SessionId != _sessionId ||
                !worker.Name.Equals("codex.exe", StringComparison.OrdinalIgnoreCase) ||
                !worker.CommandLine.Contains("app-server", StringComparison.OrdinalIgnoreCase) ||
                !worker.CommandLine.Contains("openai.chatgpt", StringComparison.OrdinalIgnoreCase) ||
                !processes.TryGetValue(worker.ParentId, out var host) ||
                !host.Name.Equals("Code.exe", StringComparison.OrdinalIgnoreCase) ||
                !host.CommandLine.Contains("--type=utility", StringComparison.OrdinalIgnoreCase) ||
                !MatchesProfile(host.CommandLine) ||
                !processes.TryGetValue(host.ParentId, out var window) ||
                !window.Name.Equals("Code.exe", StringComparison.OrdinalIgnoreCase) ||
                window.MainWindowHandle == 0 ||
                string.IsNullOrWhiteSpace(window.MainWindowTitle) ||
                window.SessionId != _sessionId ||
                host.SessionId != _sessionId ||
                worker.StartedAtUtc < host.StartedAtUtc ||
                host.StartedAtUtc < window.StartedAtUtc)
            {
                continue;
            }

            targets.Add(new CodexWorkerTarget(
                worker.Id,
                worker.StartedAtUtc,
                window.Id,
                window.StartedAtUtc,
                window.MainWindowHandle,
                window.MainWindowTitle));
        }

        return targets.OrderBy(target => target.WindowTitle).ToList();
    }

    public void ValidateReady(CodexWorkerTarget target)
    {
        var current = FindTargets().SingleOrDefault(candidate =>
            candidate.WorkerProcessId == target.WorkerProcessId &&
            candidate.WorkerStartedAtUtc == target.WorkerStartedAtUtc &&
            candidate.WindowProcessId == target.WindowProcessId &&
            candidate.WindowStartedAtUtc == target.WindowStartedAtUtc &&
            candidate.WindowHandle == target.WindowHandle);

        if (current is null)
        {
            throw new InvalidOperationException(
                $"The Codex worker in {target.WindowTitle} changed. Try the switch again.");
        }

        if (_recoveryUi.IsTurnRunning(target.WindowHandle))
        {
            throw new InvalidOperationException(
                $"A Codex turn is still running in {target.WindowTitle}. Wait for it to finish.");
        }
    }

    public async Task RestartAsync(
        CodexWorkerTarget target,
        CancellationToken cancellationToken = default)
    {
        ValidateReady(target);

        _inventory.Stop(target.WorkerProcessId);

        if (!await _recoveryUi.InvokeRetryAsync(target.WindowHandle, cancellationToken))
        {
            throw new InvalidOperationException(
                "Codex did not offer its recovery button. Reload this VS Code window manually.");
        }

        for (var attempt = 0; attempt < 60; attempt++)
        {
            cancellationToken.ThrowIfCancellationRequested();
            var replacements = FindTargets().Where(candidate =>
                candidate.WindowProcessId == target.WindowProcessId &&
                candidate.WindowStartedAtUtc == target.WindowStartedAtUtc &&
                candidate.WindowHandle == target.WindowHandle &&
                candidate.WorkerProcessId != target.WorkerProcessId).ToList();

            if (replacements.Count == 1)
            {
                return;
            }

            if (replacements.Count > 1)
            {
                break;
            }

            await Task.Delay(250, cancellationToken);
        }

        throw new InvalidOperationException(
            "A replacement Codex worker was not verified. Reload this VS Code window manually.");
    }

    private bool MatchesProfile(string hostCommandLine)
    {
        if (!_profileConfigured)
        {
            return true;
        }

        var match = Regex.Match(
            hostCommandLine,
            "--user-data-dir=(?:\"(?<quoted>[^\"]+)\"|(?<plain>\\S+))",
            RegexOptions.IgnoreCase);
        var path = match.Groups["quoted"].Success
            ? match.Groups["quoted"].Value
            : match.Groups["plain"].Value;

        try
        {
            return match.Success && string.Equals(
                Path.GetFullPath(path).TrimEnd(Path.DirectorySeparatorChar),
                _requiredUserDataDirectory,
                StringComparison.OrdinalIgnoreCase);
        }
        catch
        {
            return false;
        }
    }

    private static string ResolveUserDataDirectory()
    {
        var configured = Environment.GetEnvironmentVariable(
            "CODEX_ACCOUNT_SWITCHER_VSCODE_USER_DATA_DIR");
        if (!string.IsNullOrWhiteSpace(configured))
        {
            return Environment.ExpandEnvironmentVariables(configured);
        }

        if (!string.IsNullOrWhiteSpace(Environment.GetEnvironmentVariable("CODEX_HOME")))
        {
            // A custom Codex home cannot be inferred from a VS Code process.
            // Require an explicit isolated profile to avoid restarting normal windows.
            return string.Empty;
        }

        return Path.Combine(
            Environment.GetFolderPath(Environment.SpecialFolder.ApplicationData), "Code");
    }
}
