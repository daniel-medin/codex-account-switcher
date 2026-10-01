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
        if (durationMinutes is not int minutes || minutes <= 0)
        {
            return fallback;
        }

        if (minutes == 300)
        {
            return "5h";
        }

        if (minutes == 10080)
        {
            return "Weekly";
        }

        if (minutes < 1440 && minutes % 60 == 0)
        {
            return $"{minutes / 60}h";
        }

        if (minutes >= 1440 && minutes % 1440 == 0)
        {
            return $"{minutes / 1440}d";
        }

        return $"{minutes} min";
    }
}
