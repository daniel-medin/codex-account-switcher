namespace CodexAccountSwitcher.Models;

public sealed record CodexWorkerTarget(
    int WorkerProcessId,
    DateTime WorkerStartedAtUtc,
    int WindowProcessId,
    DateTime WindowStartedAtUtc,
    nint WindowHandle,
    string WindowTitle)
{
    public string DisplayName => $"{WindowTitle} (VS Code {WindowProcessId})";

    public override string ToString() => DisplayName;
}
