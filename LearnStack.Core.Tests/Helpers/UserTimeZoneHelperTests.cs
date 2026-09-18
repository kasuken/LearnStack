using LearnStack.Core.Helpers;

namespace LearnStack.Core.Tests.Helpers;

public class UserTimeZoneHelperTests
{
    // Etc/GMT-13 is a fixed UTC+13 offset with no DST, matching a real-world zone
    // such as Pacific/Auckland during its daylight-saving period.
    private const string Utc13ZoneId = "Etc/GMT-13";

    // Etc/GMT+8 is a fixed UTC-8 offset with no DST, matching a real-world zone
    // such as America/Los_Angeles during standard time.
    private const string UtcMinus8ZoneId = "Etc/GMT+8";

    [Fact]
    public void ResolveTimeZone_WithNullOrEmpty_FallsBackToUtc()
    {
        Assert.Equal(TimeZoneInfo.Utc, UserTimeZoneHelper.ResolveTimeZone(null));
        Assert.Equal(TimeZoneInfo.Utc, UserTimeZoneHelper.ResolveTimeZone(""));
        Assert.Equal(TimeZoneInfo.Utc, UserTimeZoneHelper.ResolveTimeZone("   "));
    }

    [Fact]
    public void ResolveTimeZone_WithUnknownId_FallsBackToUtc()
    {
        Assert.Equal(TimeZoneInfo.Utc, UserTimeZoneHelper.ResolveTimeZone("Not/A_Real_Zone"));
    }

    [Fact]
    public void ResolveTimeZone_WithValidIanaId_ResolvesTimeZone()
    {
        var timeZone = UserTimeZoneHelper.ResolveTimeZone(Utc13ZoneId);

        Assert.Equal(TimeSpan.FromHours(13), timeZone.BaseUtcOffset);
    }

    [Fact]
    public void GetDaysAgo_ForUserAheadOfUtc_RollsOverToNextLocalDayBeforeUtcMidnight()
    {
        // Both timestamps fall on 2026-01-01 in UTC, but for a user in UTC+13
        // (e.g. Pacific/Auckland) local midnight arrives 13 hours earlier, so
        // "now" (12:00 UTC -> 01:00 local the next day) has already rolled over
        // to a new local calendar day relative to the resource (05:00 UTC -> 18:00
        // local, still the same local day).
        var resourceUtc = new DateTime(2026, 1, 1, 5, 0, 0, DateTimeKind.Utc);
        var nowUtc = new DateTime(2026, 1, 1, 12, 0, 0, DateTimeKind.Utc);

        // In UTC, both timestamps fall on the same calendar day (0 days ago).
        Assert.Equal(0, UserTimeZoneHelper.GetDaysAgo(resourceUtc, nowUtc, "UTC"));

        // In UTC+13, "now" has already rolled over to the next local day, so the
        // resource (added earlier that local day) is "yesterday" relative to it.
        Assert.Equal(1, UserTimeZoneHelper.GetDaysAgo(resourceUtc, nowUtc, Utc13ZoneId));
    }

    [Fact]
    public void GetDaysAgo_ForUserBehindUtc_StaysOnPreviousDayAfterUtcMidnight()
    {
        // 2026-01-02 03:00 UTC is already 2026-01-02 in UTC, but it's still
        // 2026-01-01 19:00 local time for a user in UTC-8 (e.g. Los_Angeles).
        var resourceUtc = new DateTime(2026, 1, 1, 20, 0, 0, DateTimeKind.Utc);
        var nowUtc = new DateTime(2026, 1, 2, 3, 0, 0, DateTimeKind.Utc);

        // In UTC, the resource was added the previous calendar day (1 day ago).
        Assert.Equal(1, UserTimeZoneHelper.GetDaysAgo(resourceUtc, nowUtc, "UTC"));

        // In UTC-8, "now" hasn't rolled over to the next local day yet, so the
        // resource (added earlier that same local day) is still "today".
        Assert.Equal(0, UserTimeZoneHelper.GetDaysAgo(resourceUtc, nowUtc, UtcMinus8ZoneId));
    }

    [Fact]
    public void ConvertUtcToUserLocal_RoundTripsThroughConvertUserLocalToUtc()
    {
        var timeZone = UserTimeZoneHelper.ResolveTimeZone(UtcMinus8ZoneId);
        var utcNow = new DateTime(2026, 6, 15, 4, 0, 0, DateTimeKind.Utc);

        var local = UserTimeZoneHelper.ConvertUtcToUserLocal(utcNow, timeZone);
        var backToUtc = UserTimeZoneHelper.ConvertUserLocalToUtc(local, timeZone);

        Assert.Equal(utcNow, backToUtc);
    }
}
