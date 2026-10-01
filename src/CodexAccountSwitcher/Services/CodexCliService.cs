using System.Diagnostics;

namespace CodexAccountSwitcher.Services;

public sealed class CodexCliService : ICodexCliService
{
    public async Task<string?> FindCodexExecutableAsync(CancellationToken cancellationToken = default)
    {
        var pathValue = Environment.GetEnvironmentVariable("PATH");

        if (!string.IsNullOrWhiteSpace(pathValue))
        {
            foreach (var directory in pathValue.Split(Path.PathSeparator, StringSplitOptions.RemoveEmptyEntries))
            {
                try
                {
                    var candidate = Path.Combine(directory.Trim().Trim('"'), "codex.exe");
                    if (File.Exists(candidate))
                    {
                        return candidate;
                    }
                }
                catch
                {
                    // Ignore malformed PATH entries.
                }
            }
        }

        try
        {
            using var process = new Process
            {
                StartInfo = new ProcessStartInfo
                {
                    FileName = "where.exe",
                    Arguments = "codex",
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

    public async Task<int> RunInteractiveLoginAsync(
        string executablePath,
        CancellationToken cancellationToken = default)
    {
        var escapedExecutable = executablePath.Replace(""", """");

        using var process = new Process
        {
            StartInfo = new ProcessStartInfo
            {
                FileName = "cmd.exe",
                Arguments = $"/c "\"{escapedExecutable}\" login"",
                UseShellExecute = true,
                CreateNoWindow = false,
                WindowStyle = ProcessWindowStyle.Normal
            }
        };

        process.Start();
        await process.WaitForExitAsync(cancellationToken);
        return process.ExitCode;
    }
}
