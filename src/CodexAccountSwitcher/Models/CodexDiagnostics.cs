namespace CodexAccountSwitcher.Models;

public sealed class CodexDiagnostics
{
    public string CodexHome { get; init; } = "Not detected";
    public bool CodexHomeExists { get; init; }
    public string CodexExecutable { get; init; } = "Not detected";
    public string CodexVersion { get; init; } = "Unknown";
    public string AuthCandidate { get; init; } = "Not detected";
    public string SessionsDirectory { get; init; } = "Not detected";
    public string SkillsDirectory { get; init; } = "Not detected";
    public int VsCodeProcessCount { get; init; }
    public int CodexProcessCount { get; init; }
}
