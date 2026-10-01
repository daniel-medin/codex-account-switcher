using CodexAccountSwitcher.Models;

namespace CodexAccountSwitcher.Tests;

public sealed class WeeklySpendTextTests
{
    private static readonly DateTimeOffset Now = new(2026, 10, 1, 12, 0, 0, TimeSpan.Zero);

    [Theory]
    [InlineData(2, 3, 0, "2d 3h left to use the remaining 24%")]
    [InlineData(0, 4, 30, "4h 30m left to use the remaining 24%")]
    [InlineData(0, 0, 15, "15m left to use the remaining 24%")]
    public void ShowsTimeUntilWeeklyReset(int days, int hours, int minutes, string expected)
    {
        var usage = new CodexUsage
        {
            SecondaryUsedPercent = 76,
            SecondaryWindowDurationMinutes = 10080,
            SecondaryResetAt = Now.AddDays(days).AddHours(hours).AddMinutes(minutes)
        };

        Assert.Equal(expected, usage.GetWeeklySpendText(Now));
    }

    [Fact]
    public void HidesCountdownWhenThereIsNothingToSpendOrResetIsUnknown()
    {
        var exhausted = new CodexUsage
        {
            SecondaryUsedPercent = 100,
            SecondaryWindowDurationMinutes = 10080,
            SecondaryResetAt = Now.AddDays(2)
        };
        var expired = new CodexUsage
        {
            SecondaryUsedPercent = 25,
            SecondaryWindowDurationMinutes = 10080,
            SecondaryResetAt = Now
        };
        var otherWindow = new CodexUsage
        {
            SecondaryUsedPercent = 25,
            SecondaryWindowDurationMinutes = 300,
            SecondaryResetAt = Now.AddHours(2)
        };

        Assert.Empty(exhausted.GetWeeklySpendText(Now));
        Assert.Empty(expired.GetWeeklySpendText(Now));
        Assert.Empty(otherWindow.GetWeeklySpendText(Now));
        Assert.Empty(new CodexUsage().GetWeeklySpendText(Now));
    }
}
