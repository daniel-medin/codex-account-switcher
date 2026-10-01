namespace CodexAccountSwitcher.Models;

public sealed class CodexAccount
{
    public Guid Id { get; set; } = Guid.NewGuid();
    public string DisplayName { get; set; } = string.Empty;
    public DateTimeOffset CreatedAt { get; set; } = DateTimeOffset.UtcNow;
    public DateTimeOffset? LastUsedAt { get; set; }
    public string CredentialFileName { get; set; } = string.Empty;

    public bool IsActive { get; set; }
}
