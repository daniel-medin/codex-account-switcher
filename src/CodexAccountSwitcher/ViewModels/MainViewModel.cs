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
    private CodexDiagnostics _diagnostics = new();
    private string _statusText = "Ready.";
    private string _accountName = string.Empty;
    private bool _isBusy;

    public MainViewModel(
        ICodexEnvironmentService environmentService,
        ICodexProcessService processService,
        IAccountManagerService accountManager)
    {
        _environmentService = environmentService;
        _accountManager = accountManager;

        _ = processService;

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
            "Refreshing diagnostics and accounts...",
            async () =>
            {
                Diagnostics = await _environmentService.GetDiagnosticsAsync();
                await ReloadAccountsAsync();
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
                await ReloadAccountsAsync();
                StatusText = $"Imported {account.DisplayName}.";
            });
    }

    private async Task AddAccountAsync()
    {
        await RunBusyAsync(
            "Starting Codex login for another account...",
            async () =>
            {
                var account = await _accountManager.AddAccountViaLoginAsync(AccountName);
                AccountName = string.Empty;
                await ReloadAccountsAsync();
                Diagnostics = await _environmentService.GetDiagnosticsAsync();
                StatusText = $"Added {account.DisplayName}. The new account is now active.";
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
                await ReloadAccountsAsync();
                StatusText =
                    $"Switched auth to {account.DisplayName}. Reload the Codex VS Code window/session so it picks up the new credentials.";
            });
    }

    private async Task ReloadAccountsAsync()
    {
        var accounts = await _accountManager.GetAccountsAsync();

        Accounts.Clear();
        foreach (var account in accounts.OrderBy(account => account.CreatedAt))
        {
            Accounts.Add(account);
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
