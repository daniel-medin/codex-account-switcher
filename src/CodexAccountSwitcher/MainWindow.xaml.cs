using System.Net.Http;
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

        var processService = new CodexProcessService();
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

        var vsCodeService = new VsCodeService();

        DataContext = new MainViewModel(
            environmentService,
            accountManager,
            usageService,
            vsCodeService);
    }
}
