using System.Diagnostics;
using System.Management;

namespace CodexAccountSwitcher.Services;

public sealed record WindowsProcessSnapshot(
    int Id,
    int ParentId,
    int SessionId,
    string Name,
    string CommandLine,
    DateTime StartedAtUtc,
    nint MainWindowHandle,
    string MainWindowTitle);

public interface IWindowsProcessInventory
{
    IReadOnlyList<WindowsProcessSnapshot> Read();
    void Stop(int processId);
}

public sealed class WindowsProcessInventory : IWindowsProcessInventory
{
    public IReadOnlyList<WindowsProcessSnapshot> Read()
    {
        var snapshots = new List<WindowsProcessSnapshot>();
        using var searcher = new ManagementObjectSearcher(
            "SELECT ProcessId, ParentProcessId, Name, CommandLine, SessionId FROM Win32_Process");
        using var results = searcher.Get();

        foreach (ManagementObject item in results)
        {
            try
            {
                var id = Convert.ToInt32(item["ProcessId"]);
                using var process = Process.GetProcessById(id);
                snapshots.Add(new WindowsProcessSnapshot(
                    id,
                    Convert.ToInt32(item["ParentProcessId"]),
                    Convert.ToInt32(item["SessionId"]),
                    (string?)item["Name"] ?? string.Empty,
                    (string?)item["CommandLine"] ?? string.Empty,
                    process.StartTime.ToUniversalTime(),
                    process.MainWindowHandle,
                    process.MainWindowTitle));
            }
            catch
            {
                // Processes can exit or deny access while the snapshot is built.
            }
            finally
            {
                item.Dispose();
            }
        }

        return snapshots;
    }

    public void Stop(int processId)
    {
        using var process = Process.GetProcessById(processId);
        process.Kill(entireProcessTree: false);
        process.WaitForExit(5000);
    }
}
