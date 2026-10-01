using System.Diagnostics;
using System.Runtime.InteropServices;

namespace CodexAccountSwitcher.Services;

public sealed class VsCodeService : IVsCodeService
{
    public async Task<int> ReloadCodexProcessesAsync(
        CancellationToken cancellationToken = default)
    {
        var targets = Process.GetProcesses()
            .Where(IsCodexProcess)
            .Where(process => HasVsCodeAncestor(process.Id))
            .ToList();

        var stopped = 0;

        foreach (var process in targets)
        {
            try
            {
                if (process.HasExited)
                {
                    continue;
                }

                process.Kill(entireProcessTree: true);
                stopped++;

                using var timeout = CancellationTokenSource.CreateLinkedTokenSource(cancellationToken);
                timeout.CancelAfter(TimeSpan.FromSeconds(5));

                try
                {
                    await process.WaitForExitAsync(timeout.Token);
                }
                catch (OperationCanceledException) when (!cancellationToken.IsCancellationRequested)
                {
                    // The process was asked to stop. VS Code will recover it as needed.
                }
            }
            catch
            {
                // Never kill unrelated processes just because inspection failed.
            }
            finally
            {
                process.Dispose();
            }
        }

        return stopped;
    }

    private static bool IsCodexProcess(Process process)
    {
        try
        {
            return process.ProcessName.Contains(
                "codex",
                StringComparison.OrdinalIgnoreCase);
        }
        catch
        {
            return false;
        }
    }

    private static bool HasVsCodeAncestor(int processId)
    {
        var currentId = processId;

        for (var depth = 0; depth < 12; depth++)
        {
            var parentId = TryGetParentProcessId(currentId);
            if (parentId is null || parentId <= 0 || parentId == currentId)
            {
                return false;
            }

            try
            {
                using var parent = Process.GetProcessById(parentId.Value);

                if (string.Equals(
                        parent.ProcessName,
                        "Code",
                        StringComparison.OrdinalIgnoreCase))
                {
                    return true;
                }

                currentId = parent.Id;
            }
            catch
            {
                return false;
            }
        }

        return false;
    }

    private static int? TryGetParentProcessId(int processId)
    {
        try
        {
            using var process = Process.GetProcessById(processId);
            var info = new ProcessBasicInformation();

            var status = NtQueryInformationProcess(
                process.Handle,
                0,
                ref info,
                Marshal.SizeOf<ProcessBasicInformation>(),
                out _);

            if (status != 0)
            {
                return null;
            }

            return info.InheritedFromUniqueProcessId.ToInt32();
        }
        catch
        {
            return null;
        }
    }

    [DllImport("ntdll.dll")]
    private static extern int NtQueryInformationProcess(
        IntPtr processHandle,
        int processInformationClass,
        ref ProcessBasicInformation processInformation,
        int processInformationLength,
        out int returnLength);

    [StructLayout(LayoutKind.Sequential)]
    private struct ProcessBasicInformation
    {
        public IntPtr Reserved1;
        public IntPtr PebBaseAddress;
        public IntPtr Reserved2_0;
        public IntPtr Reserved2_1;
        public IntPtr UniqueProcessId;
        public IntPtr InheritedFromUniqueProcessId;
    }
}
