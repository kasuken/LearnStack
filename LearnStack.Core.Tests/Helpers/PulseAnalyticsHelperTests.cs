using LearnStack.Core.Helpers;

namespace LearnStack.Core.Tests.Helpers;

public class PulseAnalyticsHelperTests
{
    // Fixed UTC+13 offset (no DST), matching a real-world zone such as
    // Pacific/Auckland during its daylight-saving period.
    private const string Utc13ZoneId = "Etc/GMT-13";

    // "Now" is comfortably after the week boundary in both UTC and UTC+13, so
    // both zones agree the current local/UTC week starts 2026-03-09 (Monday).
    private static readonly DateTime NowUtc = new(2026, 3, 9, 5, 0, 0, DateTimeKind.Utc);

    // This completion lands in the ~13 hour gap between when the *local*
    // (UTC+13) week rolls over to Monday 2026-03-09 (at 2026-03-08 11:00 UTC)
    // and when the *UTC* week rolls over to the same Monday (at 2026-03-09
    // 00:00 UTC). A UTC-day/week bucketer would count it against last week;
    // the user, already in the new local week, expects it counted this week.
    private static readonly List<DateTime> BoundaryCompletionUtc =
    [
        new(2026, 3, 8, 15, 0, 0, DateTimeKind.Utc)
    ];

    [Fact]
    public void GetWeeklyCompletionCounts_NearWeekBoundary_BucketsByLocalWeekNotUtcWeek()
    {
        var counts = PulseAnalyticsHelper.GetWeeklyCompletionCounts(BoundaryCompletionUtc, NowUtc, Utc13ZoneId, weekCount: 2);

        // Counted in the current week (index 1 of 2), matching the local calendar.
        Assert.Equal([0, 1], counts);
    }

    [Fact]
    public void GetWeeklyCompletionCounts_SameScenario_MisbucketsUnderUtcWeeks()
    {
        // Sanity check that the scenario above is a genuine UTC/local mismatch:
        // bucketing the identical completion against plain UTC week boundaries
        // puts it in the *previous* week instead of the current one.
        var counts = PulseAnalyticsHelper.GetWeeklyCompletionCounts(BoundaryCompletionUtc, NowUtc, "UTC", weekCount: 2);

        Assert.Equal([1, 0], counts);
    }

    [Fact]
    public void GetWeekStartDatesLocal_ReturnsMondayAlignedLocalWeeks()
    {
        var weekStarts = PulseAnalyticsHelper.GetWeekStartDatesLocal(NowUtc, Utc13ZoneId, weekCount: 2);

        Assert.Equal(
            [new DateTime(2026, 3, 2), new DateTime(2026, 3, 9)],
            weekStarts);
        Assert.All(weekStarts, weekStart => Assert.Equal(DayOfWeek.Monday, weekStart.DayOfWeek));
    }

    [Fact]
    public void GetWeeklyCompletionCounts_WithNoCompletions_ReturnsAllZeros()
    {
        var counts = PulseAnalyticsHelper.GetWeeklyCompletionCounts([], NowUtc, Utc13ZoneId, weekCount: 3);

        Assert.Equal([0, 0, 0], counts);
    }
}
