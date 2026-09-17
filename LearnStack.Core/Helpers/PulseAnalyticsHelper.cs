namespace LearnStack.Core.Helpers;

/// <summary>
/// Pure, timezone-aware bucketing math used by the Pulse activity chart.
/// Weeks are aligned to the user's local calendar (Monday start) rather than UTC,
/// so a completion near a week boundary lands in the week the user actually
/// experienced it in.
/// </summary>
public static class PulseAnalyticsHelper
{
    /// <summary>
    /// Returns the local (unspecified-kind) start-of-week date for each of the most recent
    /// <paramref name="weekCount"/> Monday-aligned weeks, ending with the week containing
    /// <paramref name="utcNow"/>.
    /// </summary>
    public static IReadOnlyList<DateTime> GetWeekStartDatesLocal(DateTime utcNow, string? timeZoneId, int weekCount)
    {
        var timeZone = UserTimeZoneHelper.ResolveTimeZone(timeZoneId);
        var firstWeekStartLocal = GetFirstWeekStartLocal(utcNow, timeZone, weekCount);

        return Enumerable.Range(0, weekCount)
            .Select(index => firstWeekStartLocal.AddDays(index * 7))
            .ToList();
    }

    /// <summary>
    /// Counts how many of the given UTC completion timestamps fall into each of the most recent
    /// <paramref name="weekCount"/> Monday-aligned weeks in the user's local time zone.
    /// </summary>
    public static IReadOnlyList<int> GetWeeklyCompletionCounts(
        IReadOnlyCollection<DateTime> completionDatesUtc,
        DateTime utcNow,
        string? timeZoneId,
        int weekCount)
    {
        var timeZone = UserTimeZoneHelper.ResolveTimeZone(timeZoneId);
        var firstWeekStartLocal = GetFirstWeekStartLocal(utcNow, timeZone, weekCount);

        var counts = new List<int>(weekCount);
        for (var index = 0; index < weekCount; index++)
        {
            var weekStartLocal = firstWeekStartLocal.AddDays(index * 7);
            var weekEndLocal = weekStartLocal.AddDays(7);

            var weekStartUtc = UserTimeZoneHelper.ConvertUserLocalToUtc(weekStartLocal, timeZone);
            var weekEndUtc = UserTimeZoneHelper.ConvertUserLocalToUtc(weekEndLocal, timeZone);

            counts.Add(completionDatesUtc.Count(date => date >= weekStartUtc && date < weekEndUtc));
        }

        return counts;
    }

    private static DateTime GetFirstWeekStartLocal(DateTime utcNow, TimeZoneInfo timeZone, int weekCount)
    {
        var localNow = UserTimeZoneHelper.ConvertUtcToUserLocal(utcNow, timeZone);
        var daysSinceMonday = ((int)localNow.DayOfWeek + 6) % 7;
        var currentWeekStartLocal = localNow.Date.AddDays(-daysSinceMonday);

        return currentWeekStartLocal.AddDays(-7 * (weekCount - 1));
    }
}
