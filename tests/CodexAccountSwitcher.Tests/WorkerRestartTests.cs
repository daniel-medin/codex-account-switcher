using CodexAccountSwitcher.Services;

namespace CodexAccountSwitcher.Tests;

public sealed class WorkerRestartTests
{
    private static readonly DateTime Start = new(2026, 10, 1, 12, 0, 0, DateTimeKind.Utc);

    [Fact]
    public void FindsOnlyCodexWorkersBelongingToTheirOwnVsCodeWindow()
    {
        var inventory = new FakeInventory();
        var service = new CodexWorkerRestartService(inventory, new FakeRecoveryUi(inventory), 1);

        var targets = service.FindTargets();

        Assert.Equal(2, targets.Count);
        Assert.Contains(targets, target => target.WorkerProcessId == 102 && target.WindowProcessId == 100);
        Assert.Contains(targets, target => target.WorkerProcessId == 202 && target.WindowProcessId == 200);
        Assert.DoesNotContain(targets, target => target.WorkerProcessId == 302);
    }

    [Fact]
    public async Task ProfileFilterAndSequentialRefreshLeaveOtherProfileUntouched()
    {
        var inventory = new FakeInventory();
        inventory.Processes.AddRange(
        [
            new(400, 0, 1, "Code.exe", "Code.exe", Start, 4000, "Isolated - Visual Studio Code"),
            new(401, 400, 1, "Code.exe", "Code.exe --type=utility --user-data-dir=\"C:\\Profiles\\Isolated\"", Start.AddSeconds(1), 0, ""),
            new(402, 401, 1, "codex.exe", "openai.chatgpt/codex.exe app-server", Start.AddSeconds(2), 0, "")
        ]);
        inventory.Processes[1] = inventory.Processes[1] with
        {
            CommandLine = "Code.exe --type=utility --user-data-dir=\"C:\\Profiles\\Normal\""
        };
        inventory.Processes[4] = inventory.Processes[4] with
        {
            CommandLine = "Code.exe --type=utility --user-data-dir=\"C:\\Profiles\\Normal\""
        };

        var recovery = new FakeRecoveryUi(inventory);
        var service = new CodexWorkerRestartService(
            inventory, recovery, 1, @"C:\Profiles\Normal");
        var targets = service.FindTargets();

        Assert.Equal(2, targets.Count);
        foreach (var target in targets)
        {
            service.ValidateReady(target);
            await service.RestartAsync(target);
        }

        Assert.Equal([102, 202], inventory.Stopped);
        Assert.Contains(inventory.Processes, process => process.Id == 402);
    }

    [Fact]
    public async Task RestartStopsOnlySelectedWorkerAndVerifiesReplacement()
    {
        var inventory = new FakeInventory();
        var recovery = new FakeRecoveryUi(inventory);
        var service = new CodexWorkerRestartService(inventory, recovery, 1);
        var target = service.FindTargets().Single(candidate => candidate.WindowProcessId == 100);

        await service.RestartAsync(target);

        Assert.Equal([102], inventory.Stopped);
        Assert.Equal((nint)1000, recovery.InvokedWindow);
        Assert.Contains(service.FindTargets(), candidate =>
            candidate.WorkerProcessId == 103 && candidate.WindowProcessId == 100);
        Assert.Contains(service.FindTargets(), candidate =>
            candidate.WorkerProcessId == 202 && candidate.WindowProcessId == 200);
    }

    [Fact]
    public async Task RefusesStaleOrBusyWorkerBeforeStoppingAnything()
    {
        var inventory = new FakeInventory();
        var recovery = new FakeRecoveryUi(inventory);
        var service = new CodexWorkerRestartService(inventory, recovery, 1);
        var target = service.FindTargets().Single(candidate => candidate.WindowProcessId == 100);

        await Assert.ThrowsAsync<InvalidOperationException>(() =>
            service.RestartAsync(target with { WorkerStartedAtUtc = Start.AddHours(1) }));

        recovery.Busy = true;
        await Assert.ThrowsAsync<InvalidOperationException>(() => service.RestartAsync(target));
        Assert.Empty(inventory.Stopped);
    }

    [Fact]
    public async Task FailedRecoveryAsksForManualReload()
    {
        var inventory = new FakeInventory();
        var recovery = new FakeRecoveryUi(inventory) { Succeed = false };
        var service = new CodexWorkerRestartService(inventory, recovery, 1);
        var target = service.FindTargets().Single(candidate => candidate.WindowProcessId == 100);

        var error = await Assert.ThrowsAsync<InvalidOperationException>(() => service.RestartAsync(target));

        Assert.Contains("Reload", error.Message);
        Assert.Equal([102], inventory.Stopped);
    }

    private sealed class FakeInventory : IWindowsProcessInventory
    {
        public readonly List<WindowsProcessSnapshot> Processes =
        [
            new(100, 0, 1, "Code.exe", "Code.exe", Start, 1000, "Project A - Visual Studio Code"),
            new(101, 100, 1, "Code.exe", "Code.exe --type=utility", Start.AddSeconds(1), 0, ""),
            new(102, 101, 1, "codex.exe", "openai.chatgpt/codex.exe app-server", Start.AddSeconds(2), 0, ""),
            new(200, 0, 1, "Code.exe", "Code.exe", Start, 2000, "Project B - Visual Studio Code"),
            new(201, 200, 1, "Code.exe", "Code.exe --type=utility", Start.AddSeconds(1), 0, ""),
            new(202, 201, 1, "codex.exe", "openai.chatgpt/codex.exe app-server", Start.AddSeconds(2), 0, ""),
            new(300, 0, 2, "Code.exe", "Code.exe", Start, 3000, "Another session"),
            new(301, 300, 2, "Code.exe", "Code.exe --type=utility", Start.AddSeconds(1), 0, ""),
            new(302, 301, 2, "codex.exe", "openai.chatgpt/codex.exe app-server", Start.AddSeconds(2), 0, "")
        ];

        public List<int> Stopped { get; } = [];

        public IReadOnlyList<WindowsProcessSnapshot> Read() => Processes.ToList();

        public void Stop(int processId)
        {
            Stopped.Add(processId);
            Processes.RemoveAll(process => process.Id == processId);
        }
    }

    private sealed class FakeRecoveryUi(FakeInventory inventory) : ICodexRecoveryUi
    {
        public bool Busy { get; set; }
        public bool Succeed { get; set; } = true;
        public nint InvokedWindow { get; private set; }

        public bool IsTurnRunning(nint windowHandle) => Busy;

        public Task<bool> InvokeRetryAsync(nint windowHandle, CancellationToken cancellationToken)
        {
            InvokedWindow = windowHandle;
            if (Succeed)
            {
                inventory.Processes.Add(new WindowsProcessSnapshot(
                    windowHandle == 2000 ? 203 : 103,
                    windowHandle == 2000 ? 201 : 101,
                    1, "codex.exe", "openai.chatgpt/codex.exe app-server",
                    Start.AddSeconds(3), 0, ""));
            }

            return Task.FromResult(Succeed);
        }
    }
}
