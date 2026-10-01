using System.ComponentModel;
using System.Runtime.CompilerServices;
using System.Windows.Input;
using CodexAccountSwitcher.Models;
using CodexAccountSwitcher.Services;

namespace CodexAccountSwitcher.ViewModels;

public sealed class MainViewModel : INotifyPropertyChanged
{
    private readonly ICodexEnvironmentService _environmentService;
    private CodexDiagnostics _diagnostics = new();
    private string _statusText = "Ready.";

    public MainViewModel(
        ICodexEnvironmentService environmentService,
        ICodexProcessService processService)
    {
        _environmentService = environmentService;

        // Keep processService in the constructor so service composition is explicit.
        _ = processService;

        RefreshCommand = new RelayCommand(RefreshAsync);

        _ = RefreshAsync();
    }

    public CodexDiagnostics Diagnostics
    {
        get => _diagnostics;
        private set
        {
            _diagnostics = value;
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

    public ICommand RefreshCommand { get; }

    public event PropertyChangedEventHandler? PropertyChanged;

    private async Task RefreshAsync()
    {
        StatusText = "Scanning local Codex environment...";

        try
        {
            Diagnostics = await _environmentService.GetDiagnosticsAsync();
            StatusText = $"Diagnostics refreshed at {DateTime.Now:HH:mm:ss}.";
        }
        catch (Exception ex)
        {
            StatusText = $"Diagnostics failed: {ex.Message}";
        }
    }

    private void OnPropertyChanged([CallerMemberName] string? propertyName = null)
    {
        PropertyChanged?.Invoke(this, new PropertyChangedEventArgs(propertyName));
    }
}
