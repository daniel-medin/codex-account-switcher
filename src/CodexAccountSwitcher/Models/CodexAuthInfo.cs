namespace CodexAccountSwitcher.Models;

public sealed record CodexAuthInfo
{
    public string? Email { get; init; }
    public string? ChatGptUserId { get; init; }
    public string? ChatGptAccountId { get; init; }
    public string? PlanType { get; init; }
    public string AccessToken { get; init; } = string.Empty;
    public string RefreshToken { get; init; } = string.Empty;
    public string? IdToken { get; init; }
    public DateTimeOffset? AccessTokenExpiresAt { get; init; }

    public string StableIdentity =>
        !string.IsNullOrWhiteSpace(ChatGptUserId)
            ? $"user:{ChatGptUserId}|account:{ChatGptAccountId ?? string.Empty}"
            : !string.IsNullOrWhiteSpace(Email)
                ? $"email:{Email.Trim().ToLowerInvariant()}|account:{ChatGptAccountId ?? string.Empty}"
                : $"account:{ChatGptAccountId ?? string.Empty}";
}
