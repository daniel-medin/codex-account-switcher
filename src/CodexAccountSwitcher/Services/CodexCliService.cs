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

        var bundledExecutable = FindBundledExecutable();
        if (bundledExecutable is not null)
        {
            return bundledExecutable;
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

    private static string? FindBundledExecutable()
    {
        var extensionRoots = new[]
        {
            Path.Combine(
                Environment.GetFolderPath(Environment.SpecialFolder.UserProfile),
                ".vscode",
                "extensions"),
            Path.Combine(
                Environment.GetFolderPath(Environment.SpecialFolder.UserProfile),
                ".vscode-insiders",
                "extensions")
        };

        foreach (var extensionRoot in extensionRoots)
        {
            if (!Directory.Exists(extensionRoot))
            {
                continue;
            }

            try
            {
                var candidates = Directory.EnumerateDirectories(
                        extensionRoot,
                        "openai.chatgpt-*")
                    .Select(extensionDirectory => new
                    {
                        Path = Path.Combine(
                            extensionDirectory,
                            "bin",
                            "windows-x86_64",
                            "codex.exe"),
                        Version = GetExtensionVersion(extensionDirectory)
                    })
                    .Where(candidate => File.Exists(candidate.Path))
                    .OrderByDescending(candidate => candidate.Version);

                var latest = candidates.FirstOrDefault();
                if (latest is not null)
                {
                    return latest.Path;
                }
            }
            catch (IOException)
            {
                // An extension can be updated while CLI discovery is running.
            }
            catch (UnauthorizedAccessException)
            {
                // Ignore extension directories the current user cannot inspect.
            }
        }

        return null;
    }

    private static Version GetExtensionVersion(string extensionDirectory)
    {
        const string prefix = "openai.chatgpt-";
        var directoryName = Path.GetFileName(extensionDirectory);
        var versionText = directoryName[prefix.Length..].Split('-')[0];

        return Version.TryParse(versionText, out var version)
            ? version
            : new Version(0, 0);
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
                CreateNoWindow = false,
                WindowStyle = ProcessWindowStyle.Normal
            };
        }
        else
        {
            startInfo = new ProcessStartInfo
            {
                FileName = executablePath,
                UseShellExecute = false,
                CreateNoWindow = false,
                WindowStyle = ProcessWindowStyle.Normal
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
