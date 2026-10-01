using System.Text.Json.Serialization;

namespace CodexAccountSwitcher.Models;

public sealed class CodexAccount
{
    public Guid Id { get; set; } = Guid.NewGuid();
    public string DisplayName { get; set; } = string.Empty;
    public DateTimeOffset CreatedAt { get; set; } = DateTimeOffset.UtcNow;
    public DateTimeOffset? LastUsedAt { get; set; }
    public string CredentialFileName { get; set; } = string.Empty;

    public string? Email { get; set; }
    public string? ChatGptUserId { get; set; }
    public string? ChatGptAccountId { get; set; }
    public string? PlanType { get; set; }

    [JsonIgnore]
    public bool IsActive { get; set; }

    [JsonIgnore]
    public bool IsRecommended { get; set; }

    [JsonIgnore]
    public CodexUsage? Usage { get; set; }

    [JsonIgnore]
    public string IdentityText
    {
        get
        {
            var parts = new List<string>();

            if (!string.IsNullOrWhiteSpace(Email))
            {
                parts.Add(Email);
            }

            if (!string.IsNullOrWhiteSpace(PlanType))
            {
                parts.Add(PlanType);
            }

            return parts.Count == 0 ? "ChatGPT account" : string.Join(" · ", parts);
        }
    }

    [JsonIgnore]
    public string PrimaryUsageText =>
        Usage?.PrimaryRemainingPercent is double remaining
            ? $"{Usage.PrimaryLabel}: {remaining:0}% remaining"
            : "Primary: —";

    [JsonIgnore]
    public string SecondaryUsageText =>
        Usage?.SecondaryRemainingPercent is double remaining
            ? $"{Usage.SecondaryLabel}: {remaining:0}% remaining"
            : "Secondary: —";

    [JsonIgnore]
    public double PrimaryRemainingValue => Usage?.PrimaryRemainingPercent ?? 0d;

    [JsonIgnore]
    public double SecondaryRemainingValue => Usage?.SecondaryRemainingPercent ?? 0d;

    [JsonIgnore]
    public string PrimaryResetText =>
        Usage?.PrimaryResetAt is DateTimeOffset value
            ? $"Reset {value.ToLocalTime():ddd HH:mm}"
            : string.Empty;

    [JsonIgnore]
    public string SecondaryResetText =>
        Usage?.SecondaryResetAt is DateTimeOffset value
            ? $"Reset {value.ToLocalTime():ddd HH:mm}"
            : string.Empty;

    [JsonIgnore]
    public string UsageStatusText =>
        !string.IsNullOrWhiteSpace(Usage?.Error)
            ? Usage.Error!
            : Usage is null
                ? "Usage not loaded"
                : $"Updated {Usage.RetrievedAt.ToLocalTime():HH:mm:ss}";
}
