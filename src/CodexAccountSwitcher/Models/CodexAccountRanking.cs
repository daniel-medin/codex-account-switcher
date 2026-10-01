namespace CodexAccountSwitcher.Models;

public static class CodexAccountRanking
{
    public static IReadOnlyList<CodexAccount> BySpendUrgency(
        IEnumerable<CodexAccount> accounts,
        DateTimeOffset now) =>
        accounts
            .OrderBy(account => Priority(account, now))
            .ThenBy(account =>
                account.Usage?.SecondaryResetAt is DateTimeOffset resetAt && resetAt > now
                    ? resetAt
                    : DateTimeOffset.MaxValue)
            .ThenByDescending(account => account.Usage?.SecondaryRemainingPercent ?? -1)
            .ThenByDescending(account => account.Usage?.PrimaryRemainingPercent ?? -1)
            .ThenBy(account => account.CreatedAt)
            .ToList();

    public static bool HasUsableCapacity(CodexAccount account) =>
        account.Usage?.Error is null &&
        account.Usage?.PrimaryRemainingPercent is > 0 &&
        account.Usage?.SecondaryRemainingPercent is > 0;

    private static int Priority(CodexAccount account, DateTimeOffset now)
    {
        var weeklyRemaining = account.Usage?.Error is null &&
                              account.Usage?.SecondaryRemainingPercent is > 0;
        var futureWeeklyReset = account.Usage?.SecondaryResetAt is DateTimeOffset resetAt &&
                                resetAt > now;

        if (HasUsableCapacity(account))
        {
            return futureWeeklyReset ? 0 : 1;
        }

        if (weeklyRemaining)
        {
            return futureWeeklyReset ? 2 : 3;
        }

        return 4;
    }
}
