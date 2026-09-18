using LearnStack.Core.Helpers;
using LearnStack.Resources;
using Microsoft.Extensions.Localization;

namespace LearnStack;

/// <summary>
/// Shared helpers for formatting dates in a locale-aware, human-readable way.
/// </summary>
internal static class DateTimeHelper
{
    /// <summary>
    /// Returns a locale-aware relative date string (e.g. "2 days ago", "3 weeks ago"),
    /// computed against the user's local calendar day rather than UTC so "Today" and
    /// "Yesterday" flip at local midnight.
    /// Expects <paramref name="date"/> to be stored in UTC.
    /// </summary>
    /// <param name="date">The UTC timestamp to describe.</param>
    /// <param name="localizer">Localizer used to translate the relative date phrase.</param>
    /// <param name="timeZoneId">The user's IANA time zone id, or null/empty to fall back to UTC.</param>
    public static string GetRelativeDate(DateTime date, IStringLocalizer<SharedResource> localizer, string? timeZoneId = null)
        => GetRelativeDate(date, localizer, timeZoneId, DateTime.UtcNow);

    /// <summary>
    /// Overload accepting an explicit "now" for testability.
    /// </summary>
    internal static string GetRelativeDate(DateTime date, IStringLocalizer<SharedResource> localizer, string? timeZoneId, DateTime utcNow)
    {
        var daysAgo = UserTimeZoneHelper.GetDaysAgo(date, utcNow, timeZoneId);

        if (daysAgo <= 0) return localizer["Today"];
        if (daysAgo == 1) return localizer["Yesterday"];
        if (daysAgo < 7) return localizer["DaysAgo", daysAgo];
        if (daysAgo < 30) return localizer["WeeksAgo", daysAgo / 7];
        if (daysAgo < 365) return localizer["MonthsAgo", daysAgo / 30];
        return localizer["YearsAgo", daysAgo / 365];
    }
}
