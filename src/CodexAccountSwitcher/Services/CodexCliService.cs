using System.Diagnostics;

namespace CodexAccountSwitcher.Services;

public sealed class CodexCliService : ICodexCliService
{
    private static readonly string[] CandidateNames =
    [
        "codex.exe",
        "codex.cmd",
        "codex.bat",
        "codex"
    ];

    public async Task<string?> FindCodexExecutableAsync(
        CancellationToken cancellationToken = default)
    {
        var pathValue = Environment.GetEnvironmentVariable("PATH");

        if (!string.IsNullOrWhiteSpace(pathValue))
        {
            foreach (var directory in pathValue.Split(
                         Path.PathSeparator,
                         StringSplitOptions.RemoveEmptyEntries))
            {
                foreach (var candidateName in CandidateNames)
                {
                    try
                    {
                        var candidate = Path.Combine(
                            directory.Trim().Trim('"'),
                            candidateName);

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
        string codexHome,
        CancellationToken cancellationToken = default)
    {
        Directory.CreateDirectory(codexHome);

        var extension = Path.GetExtension(executablePath);
        ProcessStartInfo startInfo;

        if (extension.Equals(".cmd", StringComparison.OrdinalIgnoreCase) ||
            extension.Equals(".bat", StringComparison.OrdinalIgnoreCase))
        {
            var comSpec = Environment.GetEnvironmentVariable("ComSpec");
            startInfo = new ProcessStartInfo
            {
                FileName = string.IsNullOrWhiteSpace(comSpec) ? "cmd.exe" : comSpec,
                Arguments = $"/d /s /c \"\"{executablePath}\" login\"",
                UseShellExecute = false,
                CreateNoWindow = true
            };
        }
        else
        {
            startInfo = new ProcessStartInfo
            {
                FileName = executablePath,
                UseShellExecute = false,
                CreateNoWindow = true
            };

            startInfo.ArgumentList.Add("login");
        }

        startInfo.Environment["CODEX_HOME"] = codexHome;

        using var process = new Process { StartInfo = startInfo };
        process.Start();

        await process.WaitForExitAsync(cancellationToken);
        return process.ExitCode;
    }
}
