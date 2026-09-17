namespace LearnStack.Core.Helpers;

/// <summary>
/// Converts between UTC (how all timestamps are stored) and a user's local
/// IANA time zone (how dates should be displayed and aggregated).
/// Always resolves time zones with <see cref="TimeZoneInfo.FindSystemTimeZoneById"/>
/// using IANA identifiers, which works cross-platform on .NET via ICU.
/// Never falls back to <see cref="TimeZoneInfo.Local"/>, since server local
/// time has no relation to any individual user's time zone.
/// </summary>
public static class UserTimeZoneHelper
{
    /// <summary>
    /// Fallback used whenever a user has not yet set (or we could not resolve) a time zone.
    /// </summary>
    public const string DefaultTimeZoneId = "UTC";

    /// <summary>
    /// A curated list of common IANA time zone identifiers, suitable for populating
    /// a user-facing selector. Not exhaustive.
    /// </summary>
    public static readonly IReadOnlyList<string> CommonTimeZoneIds =
    [
        "UTC",
        "Pacific/Auckland",
        "Pacific/Honolulu",
        "America/Anchorage",
        "America/Los_Angeles",
        "America/Denver",
        "America/Chicago",
        "America/New_York",
        "America/Sao_Paulo",
        "Europe/London",
        "Europe/Paris",
        "Europe/Berlin",
        "Europe/Bucharest",
        "Europe/Moscow",
        "Africa/Cairo",
        "Africa/Johannesburg",
        "Asia/Dubai",
        "Asia/Karachi",
        "Asia/Calcutta",
        "Asia/Dhaka",
        "Asia/Bangkok",
        "Asia/Shanghai",
        "Asia/Singapore",
        "Asia/Tokyo",
        "Asia/Seoul",
        "Australia/Sydney",
    ];

    /// <summary>
    /// Resolves an IANA time zone identifier to a <see cref="TimeZoneInfo"/>.
    /// Falls back to UTC when <paramref name="timeZoneId"/> is null, empty, or unrecognized.
    /// </summary>
    public static TimeZoneInfo ResolveTimeZone(string? timeZoneId)
    {
        if (string.IsNullOrWhiteSpace(timeZoneId))
        {
            return TimeZoneInfo.Utc;
        }

        try
        {
            return TimeZoneInfo.FindSystemTimeZoneById(timeZoneId);
        }
        catch (TimeZoneNotFoundException)
        {
            return TimeZoneInfo.Utc;
        }
        catch (InvalidTimeZoneException)
        {
            return TimeZoneInfo.Utc;
        }
    }

    /// <summary>
    /// Converts a UTC timestamp to the equivalent local time in <paramref name="timeZoneId"/>.
    /// </summary>
    public static DateTime ConvertUtcToUserLocal(DateTime utcDateTime, string? timeZoneId)
        => ConvertUtcToUserLocal(utcDateTime, ResolveTimeZone(timeZoneId));

    /// <summary>
    /// Converts a UTC timestamp to the equivalent local time in <paramref name="timeZone"/>.
    /// </summary>
    public static DateTime ConvertUtcToUserLocal(DateTime utcDateTime, TimeZoneInfo timeZone)
    {
        var utc = utcDateTime.Kind == DateTimeKind.Utc
            ? utcDateTime
            : DateTime.SpecifyKind(utcDateTime.ToUniversalTime(), DateTimeKind.Utc);

        return TimeZoneInfo.ConvertTimeFromUtc(utc, timeZone);
    }

    /// <summary>
    /// Converts a local (unspecified-kind) date/time in <paramref name="timeZone"/> back to UTC.
    /// Guards against the DST "spring forward" gap by nudging invalid local times forward an hour.
    /// </summary>
    public static DateTime ConvertUserLocalToUtc(DateTime localDateTime, TimeZoneInfo timeZone)
    {
        var unspecified = DateTime.SpecifyKind(localDateTime, DateTimeKind.Unspecified);

        if (timeZone.IsInvalidTime(unspecified))
        {
            unspecified = unspecified.AddHours(1);
        }

        return TimeZoneInfo.ConvertTimeToUtc(unspecified, timeZone);
    }

    /// <summary>
    /// Returns the number of whole local calendar days between <paramref name="utcDate"/> and
    /// <paramref name="utcNow"/>, as observed in the user's time zone. Used to drive "Today" /
    /// "Yesterday" / "N days ago" style relative date formatting so the day boundary flips at
    /// local midnight rather than UTC midnight.
    /// </summary>
    public static int GetDaysAgo(DateTime utcDate, DateTime utcNow, string? timeZoneId)
    {
        var timeZone = ResolveTimeZone(timeZoneId);
        var localDate = ConvertUtcToUserLocal(utcDate, timeZone).Date;
        var localNow = ConvertUtcToUserLocal(utcNow, timeZone).Date;

        return (localNow - localDate).Days;
    }
}
