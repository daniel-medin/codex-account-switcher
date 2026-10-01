using System.Collections.ObjectModel;
using System.ComponentModel;
using System.Runtime.CompilerServices;
using System.Windows.Input;
using CodexAccountSwitcher.Models;
using CodexAccountSwitcher.Services;

namespace CodexAccountSwitcher.ViewModels;

public sealed class MainViewModel : INotifyPropertyChanged
{
    private readonly ICodexEnvironmentService _environmentService;
    private readonly IAccountManagerService _accountManager;
    private readonly ICodexUsageService _usageService;
    private readonly IVsCodeService _vsCodeService;

    private CodexDiagnostics _diagnostics = new();
    private string _statusText = "Ready.";
    private string _accountName = string.Empty;
    private bool _isBusy;

    public MainViewModel(
        ICodexEnvironmentService environmentService,
        IAccountManagerService accountManager,
        ICodexUsageService usageService,
        IVsCodeService vsCodeService)
    {
        _environmentService = environmentService;
        _accountManager = accountManager;
        _usageService = usageService;
        _vsCodeService = vsCodeService;

        RefreshCommand = new RelayCommand(RefreshAsync, () => !IsBusy);
        ImportCurrentCommand = new RelayCommand(ImportCurrentAsync, () => !IsBusy);
        AddAccountCommand = new RelayCommand(AddAccountAsync, () => !IsBusy);
        ActivateAccountCommand = new RelayCommand<CodexAccount>(
            ActivateAccountAsync,
            account => !IsBusy && account is not null && !account.IsActive);

        _ = RefreshAsync();
    }

    public ObservableCollection<CodexAccount> Accounts { get; } = new();

    public CodexDiagnostics Diagnostics
    {
        get => _diagnostics;
        private set
        {
            _diagnostics = value;
            OnPropertyChanged();
        }
    }

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

    private async Task RefreshAsync()
    {
        await RunBusyAsync(
            "Refreshing accounts and usage...",
            async () =>
            {
                Diagnostics = await _environmentService.GetDiagnosticsAsync();
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
                await _accountManager.ActivateAccountAsync(account.Id);

                var stoppedWorkers =
                    await _vsCodeService.ReloadCodexProcessesAsync();

                await ReloadAccountsAsync(refreshUsage: true);

                StatusText = stoppedWorkers > 0
                    ? $"Switched to {account.DisplayName}. Restarted {stoppedWorkers} VS Code Codex worker(s); the session should reconnect automatically."
                    : $"Switched to {account.DisplayName}. No VS Code Codex worker was running; the next Codex action will use the new account.";
            });
    }

    private async Task ReloadAccountsAsync(bool refreshUsage)
    {
        var accounts = (await _accountManager.GetAccountsAsync())
            .OrderBy(account => account.CreatedAt)
            .ToList();

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
            MarkRecommendedAccount(accounts);
        }

        Accounts.Clear();

        foreach (var account in accounts)
        {
            Accounts.Add(account);
        }
    }

    private static void MarkRecommendedAccount(List<CodexAccount> accounts)
    {
        foreach (var account in accounts)
        {
            account.IsRecommended = false;
        }

        var recommended = accounts
            .Where(account =>
                account.Usage?.Error is null &&
                account.Usage?.PrimaryRemainingPercent is not null)
            .OrderByDescending(account =>
                account.Usage!.PrimaryRemainingPercent ?? -1)
            .ThenByDescending(account =>
                account.Usage!.SecondaryRemainingPercent ?? -1)
            .FirstOrDefault();

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
