using System.Diagnostics;

namespace CodexAccountSwitcher.Services;

public sealed class CodexProcessService : ICodexProcessService
{
    public int CountVsCodeProcesses()
    {
        return Process.GetProcesses()
            .Count(process => IsProcessNamed(process, "Code"));
    }

    public int CountCodexLikeProcesses()
    {
        return Process.GetProcesses()
            .Count(process =>
                IsProcessNamed(process, "codex") ||
                ProcessNameContains(process, "codex"));
    }

    private static bool IsProcessNamed(Process process, string expected)
    {
        try
        {
            return string.Equals(process.ProcessName, expected, StringComparison.OrdinalIgnoreCase);
        }
        catch
        {
            return false;
        }
    }

    private static bool ProcessNameContains(Process process, string value)
    {
        try
        {
            return process.ProcessName.Contains(value, StringComparison.OrdinalIgnoreCase);
        }
        catch
        {
            return false;
        }
    }
}
