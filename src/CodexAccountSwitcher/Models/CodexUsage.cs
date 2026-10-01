namespace CodexAccountSwitcher.Models;

public sealed class CodexUsage
{
    public double? PrimaryUsedPercent { get; init; }
    public int? PrimaryWindowDurationMinutes { get; init; }
    public DateTimeOffset? PrimaryResetAt { get; init; }

    public double? SecondaryUsedPercent { get; init; }
    public int? SecondaryWindowDurationMinutes { get; init; }
    public DateTimeOffset? SecondaryResetAt { get; init; }

    public string? PlanType { get; init; }
    public string? Error { get; init; }
    public DateTimeOffset RetrievedAt { get; init; } = DateTimeOffset.UtcNow;

    public double? PrimaryRemainingPercent =>
        PrimaryUsedPercent is null ? null : Math.Clamp(100d - PrimaryUsedPercent.Value, 0d, 100d);

    public double? SecondaryRemainingPercent =>
        SecondaryUsedPercent is null ? null : Math.Clamp(100d - SecondaryUsedPercent.Value, 0d, 100d);

    public string PrimaryLabel => FormatWindowLabel(PrimaryWindowDurationMinutes, "Primary");
    public string SecondaryLabel => FormatWindowLabel(SecondaryWindowDurationMinutes, "Secondary");

    private static string FormatWindowLabel(int? durationMinutes, string fallback)
    {
        return durationMinutes switch
        {
            300 => "5h",
            10080 => "Weekly",
            > 0 and < 1440 when durationMinutes.Value % 60 == 0 => $"{durationMinutes.Value / 60}h",
            >= 1440 when durationMinutes.Value % 1440 == 0 => $"{durationMinutes.Value / 1440}d",
            > 0 => $"{durationMinutes} min",
            _ => fallback
        };
    }
}
