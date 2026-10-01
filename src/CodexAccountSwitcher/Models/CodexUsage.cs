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

    public string GetWeeklySpendText(DateTimeOffset now)
    {
        if (Error is not null ||
            SecondaryWindowDurationMinutes != 10080 ||
            SecondaryResetAt is not DateTimeOffset resetAt ||
            SecondaryRemainingPercent is not double remaining ||
            remaining <= 0 ||
            resetAt <= now)
        {
            return string.Empty;
        }

        var minutesLeft = (int)Math.Ceiling((resetAt - now).TotalMinutes);
        var days = minutesLeft / 1440;
        var hours = (minutesLeft % 1440) / 60;
        var minutes = minutesLeft % 60;
        var timeLeft = days > 0
            ? hours > 0 ? $"{days}d {hours}h" : $"{days}d"
            : hours > 0 ? minutes > 0 ? $"{hours}h {minutes}m" : $"{hours}h"
            : $"{minutes}m";

        return $"{timeLeft} left to use the remaining {remaining:0}%";
    }

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
