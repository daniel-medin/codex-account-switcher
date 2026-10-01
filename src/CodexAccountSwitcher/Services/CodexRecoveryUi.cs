using System.Windows.Automation;

namespace CodexAccountSwitcher.Services;

public interface ICodexRecoveryUi
{
    bool IsTurnRunning(nint windowHandle);
    Task<bool> InvokeRetryAsync(nint windowHandle, CancellationToken cancellationToken);
}

public sealed class CodexRecoveryUi : ICodexRecoveryUi
{
    public bool IsTurnRunning(nint windowHandle)
    {
        var root = AutomationElement.FromHandle(windowHandle);
        var elements = root.FindAll(
            TreeScope.Descendants,
            new PropertyCondition(AutomationElement.NameProperty, "Stop"));

        for (var i = 0; i < elements.Count; i++)
        {
            if (elements[i].Current.ControlType == ControlType.Button)
            {
                return true;
            }
        }

        return false;
    }

    public async Task<bool> InvokeRetryAsync(nint windowHandle, CancellationToken cancellationToken)
    {
        for (var attempt = 0; attempt < 60; attempt++)
        {
            cancellationToken.ThrowIfCancellationRequested();
            try
            {
                var root = AutomationElement.FromHandle(windowHandle);
                var error = root.FindFirst(
                    TreeScope.Descendants,
                    new PropertyCondition(AutomationElement.NameProperty, "ChatGPT hit a snag"));
                var buttons = root.FindAll(
                    TreeScope.Descendants,
                    new AndCondition(
                        new PropertyCondition(AutomationElement.NameProperty, "Try again"),
                        new PropertyCondition(AutomationElement.ControlTypeProperty, ControlType.Button)));

                if (error is not null && buttons.Count == 1 &&
                    buttons[0].TryGetCurrentPattern(InvokePattern.Pattern, out var pattern))
                {
                    ((InvokePattern)pattern).Invoke();
                    return true;
                }
            }
            catch (ElementNotAvailableException)
            {
                // The webview may be rebuilding while it reports the worker failure.
            }

            await Task.Delay(250, cancellationToken);
        }

        return false;
    }
}
