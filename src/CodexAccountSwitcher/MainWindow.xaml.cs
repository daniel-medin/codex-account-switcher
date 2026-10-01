using CodexAccountSwitcher.Services;
using CodexAccountSwitcher.ViewModels;

namespace CodexAccountSwitcher;

public partial class MainWindow : System.Windows.Window
{
    public MainWindow()
    {
        InitializeComponent();

        var environmentService = new CodexEnvironmentService();
        var processService = new CodexProcessService();

        DataContext = new MainViewModel(environmentService, processService);
    }
}
