using CodexAccountSwitcher.Models;

namespace CodexAccountSwitcher.Tests;

public sealed class CodexAccountRankingTests
{
    private static readonly DateTimeOffset Now = new(2026, 10, 1, 12, 0, 0, TimeSpan.Zero);

    [Fact]
    public void UsableAccountWithSoonestWeeklyResetComesFirst()
    {
        var moreCapacityLater = Account("More capacity later", primaryUsed: 10, weeklyUsed: 10, resetAt: Now.AddDays(5));
        var lessCapacitySooner = Account("Less capacity sooner", primaryUsed: 70, weeklyUsed: 80, resetAt: Now.AddDays(1));
        var weeklyLeftButFiveHourExhausted = Account("Wait for five-hour reset", primaryUsed: 100, weeklyUsed: 5, resetAt: Now.AddHours(2));
        var unknown = Account("Unknown", primaryUsed: null, weeklyUsed: null, resetAt: null);

        var sorted = CodexAccountRanking.BySpendUrgency(
            [moreCapacityLater, unknown, weeklyLeftButFiveHourExhausted, lessCapacitySooner],
            Now);

        Assert.Equal(
            ["Less capacity sooner", "More capacity later", "Wait for five-hour reset", "Unknown"],
            sorted.Select(account => account.DisplayName));
        Assert.True(CodexAccountRanking.HasUsableCapacity(sorted[0]));
        Assert.False(CodexAccountRanking.HasUsableCapacity(weeklyLeftButFiveHourExhausted));
    }

    [Fact]
    public void AccountsWithoutFutureWeeklyResetComeAfterTimedUsableAccounts()
    {
        var timed = Account("Timed", 50, 50, Now.AddDays(3));
        var stale = Account("Stale reset", 50, 50, Now.AddMinutes(-1));
        var noReset = Account("No reset time", 50, 50, null);

        var sorted = CodexAccountRanking.BySpendUrgency([noReset, stale, timed], Now);

        Assert.Same(timed, sorted[0]);
    }

    private static CodexAccount Account(
        string name,
        double? primaryUsed,
        double? weeklyUsed,
        DateTimeOffset? resetAt) => new()
    {
        DisplayName = name,
        Usage = new CodexUsage
        {
            PrimaryUsedPercent = primaryUsed,
            SecondaryUsedPercent = weeklyUsed,
            SecondaryResetAt = resetAt
        }
    };
}
