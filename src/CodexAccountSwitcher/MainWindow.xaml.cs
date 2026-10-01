using System.Net.Http;
using CodexAccountSwitcher.Models;
using CodexAccountSwitcher.Services;
using CodexAccountSwitcher.ViewModels;

namespace CodexAccountSwitcher;

public partial class MainWindow : System.Windows.Window
{
    private static readonly HttpClient HttpClient = new()
    {
        Timeout = TimeSpan.FromSeconds(15)
    };

    public MainWindow()
    {
        InitializeComponent();

        if (!string.IsNullOrWhiteSpace(
                Environment.GetEnvironmentVariable("CODEX_ACCOUNT_SWITCHER_DATA_HOME")))
        {
            Title = "Codex Account Switcher — Isolated Test";
        }

        var processService = new CodexProcessService();
        var workerRestartService = new CodexWorkerRestartService();
        var environmentService = new CodexEnvironmentService(processService);
        var accountStore = new AccountStoreService();
        var authParser = new CodexAuthParser();
        var codexCli = new CodexCliService();

        var accountManager = new AccountManagerService(
            accountStore,
            codexCli,
            authParser);

        var usageService = new CodexUsageService(
            accountStore,
            authParser,
            HttpClient);

        DataContext = new MainViewModel(
            environmentService,
            accountManager,
            usageService,
            workerRestartService);
    }

    private async void EditAlias_Click(object sender, System.Windows.RoutedEventArgs e)
    {
        if (DataContext is not MainViewModel viewModel ||
            viewModel.IsBusy ||
            sender is not System.Windows.FrameworkElement { DataContext: CodexAccount account })
        {
            return;
        }

        var dialog = new RenameAccountWindow(account.DisplayName)
        {
            Owner = this
        };

        if (dialog.ShowDialog() == true)
        {
            await viewModel.RenameAccountAsync(account, dialog.AccountName);
        }
    }
}
