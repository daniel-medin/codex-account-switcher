using CodexAccountSwitcher.Services;
using CodexAccountSwitcher.ViewModels;

namespace CodexAccountSwitcher;

public partial class MainWindow : System.Windows.Window
{
    public MainWindow()
    {
        InitializeComponent();

        var processService = new CodexProcessService();
        var environmentService = new CodexEnvironmentService(processService);
        var accountStore = new AccountStoreService();
        var codexCli = new CodexCliService();
        var accountManager = new AccountManagerService(accountStore, codexCli);

        DataContext = new MainViewModel(
            environmentService,
            processService,
            accountManager);
    }
}
