using System.Diagnostics;
using CodexAccountSwitcher.Models;

namespace CodexAccountSwitcher.Services;

public sealed class CodexEnvironmentService : ICodexEnvironmentService
{
    private readonly ICodexProcessService _processService;

    public CodexEnvironmentService()
        : this(new CodexProcessService())
    {
    }

    public CodexEnvironmentService(ICodexProcessService processService)
    {
        _processService = processService;
    }

    public async Task<CodexDiagnostics> GetDiagnosticsAsync(CancellationToken cancellationToken = default)
    {
        var codexHome = ResolveCodexHome();
        var codexHomeExists = Directory.Exists(codexHome);
        var executable = await FindExecutableAsync("codex.exe", cancellationToken)
                         ?? await FindExecutableAsync("codex", cancellationToken);

        var version = executable is null
            ? "Unknown"
            : await ReadVersionAsync(executable, cancellationToken);

        return new CodexDiagnostics
        {
            CodexHome = codexHome,
            CodexHomeExists = codexHomeExists,
            CodexExecutable = executable ?? "Not detected",
            CodexVersion = version,
            AuthCandidate = FindExistingPath(
                Path.Combine(codexHome, "auth.json")) ?? "Not detected",
            SessionsDirectory = FindExistingPath(
                Path.Combine(codexHome, "sessions")) ?? "Not detected",
            SkillsDirectory = FindExistingPath(
                Path.Combine(codexHome, "skills")) ?? "Not detected",
            VsCodeProcessCount = _processService.CountVsCodeProcesses(),
            CodexProcessCount = _processService.CountCodexLikeProcesses()
        };
    }

    private static string ResolveCodexHome()
    {
        var explicitHome = Environment.GetEnvironmentVariable("CODEX_HOME");
        if (!string.IsNullOrWhiteSpace(explicitHome))
        {
            return Environment.ExpandEnvironmentVariables(explicitHome);
        }

        return Path.Combine(
            Environment.GetFolderPath(Environment.SpecialFolder.UserProfile),
            ".codex");
    }

    private static string? FindExistingPath(string path)
    {
        return File.Exists(path) || Directory.Exists(path) ? path : null;
    }

    private static async Task<string?> FindExecutableAsync(
        string executableName,
        CancellationToken cancellationToken)
    {
        var pathValue = Environment.GetEnvironmentVariable("PATH");
        if (!string.IsNullOrWhiteSpace(pathValue))
        {
            foreach (var directory in pathValue.Split(Path.PathSeparator, StringSplitOptions.RemoveEmptyEntries))
            {
                try
                {
                    var trimmed = directory.Trim().Trim('"');
                    var candidate = Path.Combine(trimmed, executableName);
                    if (File.Exists(candidate))
                    {
                        return candidate;
                    }
                }
                catch
                {
                    // Diagnostics must never fail because of one malformed PATH entry.
                }
            }
        }

        return await FindWithWhereAsync(executableName, cancellationToken);
    }

    private static async Task<string?> FindWithWhereAsync(
        string executableName,
        CancellationToken cancellationToken)
    {
        try
        {
            using var process = new Process
            {
                StartInfo = new ProcessStartInfo
                {
                    FileName = "where.exe",
                    Arguments = executableName,
                    RedirectStandardOutput = true,
                    RedirectStandardError = true,
                    UseShellExecute = false,
                    CreateNoWindow = true
                }
            };

            process.Start();
            var output = await process.StandardOutput.ReadToEndAsync(cancellationToken);
            await process.WaitForExitAsync(cancellationToken);

            return output
                .Split(Environment.NewLine, StringSplitOptions.RemoveEmptyEntries)
                .Select(line => line.Trim())
                .FirstOrDefault(File.Exists);
        }
        catch
        {
            return null;
        }
    }

    private static async Task<string> ReadVersionAsync(
        string executable,
        CancellationToken cancellationToken)
    {
        try
        {
            using var process = new Process
            {
                StartInfo = new ProcessStartInfo
                {
                    FileName = executable,
                    Arguments = "--version",
                    RedirectStandardOutput = true,
                    RedirectStandardError = true,
                    UseShellExecute = false,
                    CreateNoWindow = true
                }
            };

            process.Start();

            var stdoutTask = process.StandardOutput.ReadToEndAsync(cancellationToken);
            var stderrTask = process.StandardError.ReadToEndAsync(cancellationToken);

            await process.WaitForExitAsync(cancellationToken);

            var stdout = (await stdoutTask).Trim();
            var stderr = (await stderrTask).Trim();

            if (!string.IsNullOrWhiteSpace(stdout))
            {
                return stdout;
            }

            return string.IsNullOrWhiteSpace(stderr) ? "Unknown" : stderr;
        }
        catch
        {
            return "Unknown";
        }
    }
}
