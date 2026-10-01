using System.Collections.ObjectModel;
using System.ComponentModel;
using System.Runtime.CompilerServices;
using System.Windows.Input;
using CodexAccountSwitcher.Models;
using CodexAccountSwitcher.Services;

namespace CodexAccountSwitcher.ViewModels;

public sealed class MainViewModel : INotifyPropertyChanged
{
    private readonly IAccountManagerService _accountManager;
    private readonly ICodexUsageService _usageService;
    private readonly ICodexWorkerRestartService _workerRestartService;

    private string _statusText = "Ready.";
    private string _accountName = string.Empty;
    private bool _isBusy;

    public MainViewModel(
        IAccountManagerService accountManager,
        ICodexUsageService usageService,
        ICodexWorkerRestartService workerRestartService)
    {
        _accountManager = accountManager;
        _usageService = usageService;
        _workerRestartService = workerRestartService;

        RefreshCommand = new RelayCommand(RefreshAsync, () => !IsBusy);
        ImportCurrentCommand = new RelayCommand(ImportCurrentAsync, () => !IsBusy);
        AddAccountCommand = new RelayCommand(AddAccountAsync, () => !IsBusy);
        ActivateAccountCommand = new RelayCommand<CodexAccount>(
            ActivateAccountAsync,
            account => !IsBusy && account is not null && !account.IsActive);

        _ = RefreshAsync();
    }

    public ObservableCollection<CodexAccount> Accounts { get; } = new();

    public string AccountName
    {
        get => _accountName;
        set
        {
            _accountName = value;
            OnPropertyChanged();
        }
    }

    public string StatusText
    {
        get => _statusText;
        private set
        {
            _statusText = value;
            OnPropertyChanged();
        }
    }

    public bool IsBusy
    {
        get => _isBusy;
        private set
        {
            _isBusy = value;
            OnPropertyChanged();
        }
    }

    public ICommand RefreshCommand { get; }
    public ICommand ImportCurrentCommand { get; }
    public ICommand AddAccountCommand { get; }
    public ICommand ActivateAccountCommand { get; }

    public event PropertyChangedEventHandler? PropertyChanged;

    public async Task RenameAccountAsync(CodexAccount account, string displayName)
    {
        await RunBusyAsync(
            $"Saving alias for {account.DisplayName}...",
            async () =>
            {
                await _accountManager.RenameAccountAsync(account.Id, displayName);
                account.DisplayName = displayName.Trim();
                StatusText = $"Renamed account to {account.DisplayName}.";
            });
    }

    private async Task RefreshAsync()
    {
        await RunBusyAsync(
            "Refreshing accounts and usage...",
            async () =>
            {
                await ReloadAccountsAsync(refreshUsage: true);
                StatusText = $"Refreshed at {DateTime.Now:HH:mm:ss}.";
            });
    }

    private async Task ImportCurrentAsync()
    {
        await RunBusyAsync(
            "Importing current Codex account...",
            async () =>
            {
                var account = await _accountManager.ImportCurrentAccountAsync(AccountName);
                AccountName = string.Empty;
                await ReloadAccountsAsync(refreshUsage: true);
                StatusText = $"Imported {account.DisplayName}.";
            });
    }

    private async Task AddAccountAsync()
    {
        await RunBusyAsync(
            "Opening Codex login for another account...",
            async () =>
            {
                var account = await _accountManager.AddAccountViaLoginAsync(AccountName);
                AccountName = string.Empty;
                await ReloadAccountsAsync(refreshUsage: true);

                StatusText =
                    $"Added {account.DisplayName}. Your current Codex account was left active.";
            });
    }

    private async Task ActivateAccountAsync(CodexAccount? account)
    {
        if (account is null)
        {
            return;
        }

        await RunBusyAsync(
            $"Switching to {account.DisplayName}...",
            async () =>
            {
                var targets = await Task.Run(_workerRestartService.FindTargets);
                if (targets.Count > 0)
                {
                    await Task.Run(() =>
                    {
                        foreach (var target in targets)
                        {
                            _workerRestartService.ValidateReady(target);
                        }
                    });

                    var confirmation = System.Windows.MessageBox.Show(
                        $"Switch to {account.DisplayName} and restart Codex in {targets.Count} VS Code window(s)?\n\n" +
                        "Continue only after every Codex turn has finished. " +
                        "A background turn may not be visible to the app.",
                        "Switch account and refresh Codex",
                        System.Windows.MessageBoxButton.YesNo,
                        System.Windows.MessageBoxImage.Warning);
                    if (confirmation != System.Windows.MessageBoxResult.Yes)
                    {
                        StatusText = "Account switch cancelled.";
                        return;
                    }
                }

                await _accountManager.ActivateAccountAsync(account.Id);
                var failedWindows = new List<string>();
                foreach (var target in targets)
                {
                    StatusText = $"Refreshing Codex in {target.WindowTitle}...";
                    try
                    {
                        await Task.Run(() => _workerRestartService.RestartAsync(target));
                    }
                    catch (Exception ex)
                    {
                        failedWindows.Add($"{target.WindowTitle} ({ex.Message})");
                    }
                }

                await ReloadAccountsAsync(refreshUsage: true);
                StatusText = failedWindows.Count > 0
                    ? $"Switched to {account.DisplayName}. Codex refreshed in {targets.Count - failedWindows.Count} of {targets.Count} windows. Reload manually: {string.Join(", ", failedWindows)}."
                    : targets.Count > 0
                        ? $"Switched to {account.DisplayName}. Codex restarted in all {targets.Count} detected VS Code window(s); you can continue in the same conversation."
                        : $"Switched to {account.DisplayName}. No running Codex worker was found in the matching VS Code profile. A new Codex pane will use this account; reload any existing pane that does not reconnect.";
            });
    }

    private async Task ReloadAccountsAsync(bool refreshUsage)
    {
        var accounts = (await _accountManager.GetAccountsAsync()).ToList();

        if (refreshUsage && accounts.Count > 0)
        {
            var usageTasks = accounts.Select(async account =>
            {
                account.Usage = await _usageService.GetUsageAsync(account);

                if (!string.IsNullOrWhiteSpace(account.Usage.PlanType))
                {
                    account.PlanType = account.Usage.PlanType;
                }
            });

            await Task.WhenAll(usageTasks);
        }

        var rankedAccounts = CodexAccountRanking.BySpendUrgency(accounts, DateTimeOffset.UtcNow);
        MarkRecommendedAccount(rankedAccounts);

        Accounts.Clear();

        foreach (var account in rankedAccounts)
        {
            Accounts.Add(account);
        }
    }

    private static void MarkRecommendedAccount(IReadOnlyList<CodexAccount> accounts)
    {
        foreach (var account in accounts)
        {
            account.IsRecommended = false;
        }

        var recommended = accounts.FirstOrDefault(CodexAccountRanking.HasUsableCapacity);

        if (recommended is not null)
        {
            recommended.IsRecommended = true;
        }
    }

    private async Task RunBusyAsync(string busyText, Func<Task> action)
    {
        if (IsBusy)
        {
            return;
        }

        IsBusy = true;
        StatusText = busyText;
        RaiseCommandStates();

        try
        {
            await action();
        }
        catch (Exception ex)
        {
            StatusText = ex.Message;
        }
        finally
        {
            IsBusy = false;
            RaiseCommandStates();
        }
    }

    private void RaiseCommandStates()
    {
        (RefreshCommand as RelayCommand)?.RaiseCanExecuteChanged();
        (ImportCurrentCommand as RelayCommand)?.RaiseCanExecuteChanged();
        (AddAccountCommand as RelayCommand)?.RaiseCanExecuteChanged();
        (ActivateAccountCommand as RelayCommand<CodexAccount>)?.RaiseCanExecuteChanged();
    }

    private void OnPropertyChanged([CallerMemberName] string? propertyName = null)
    {
        PropertyChanged?.Invoke(this, new PropertyChangedEventArgs(propertyName));
    }
}
