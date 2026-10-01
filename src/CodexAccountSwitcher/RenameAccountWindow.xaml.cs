namespace CodexAccountSwitcher;

public partial class RenameAccountWindow : System.Windows.Window
{
    public RenameAccountWindow(string currentAlias)
    {
        InitializeComponent();
        AliasTextBox.Text = currentAlias;
        AliasTextBox.Focus();
        AliasTextBox.SelectAll();
    }

    public string AccountName => AliasTextBox.Text.Trim();

    private void Save_Click(object sender, System.Windows.RoutedEventArgs e)
    {
        if (string.IsNullOrWhiteSpace(AliasTextBox.Text))
        {
            ErrorText.Text = "Enter an account alias.";
            AliasTextBox.Focus();
            return;
        }

        DialogResult = true;
    }
}