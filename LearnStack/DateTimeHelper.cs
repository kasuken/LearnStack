using LearnStack.Resources;
using Microsoft.Extensions.Localization;

namespace LearnStack;

/// <summary>
/// Shared helpers for formatting dates in a locale-aware, human-readable way.
/// </summary>
internal static class DateTimeHelper
{
    /// <summary>
    /// Returns a locale-aware relative date string (e.g. "2 days ago", "3 weeks ago").
    /// Expects <paramref name="date"/> to be stored in UTC.
    /// </summary>
    public static string GetRelativeDate(DateTime date, IStringLocalizer<SharedResource> localizer)
    {
        var diff = DateTime.UtcNow - date;
        if (diff.TotalDays < 1) return localizer["Today"];
        if (diff.TotalDays < 2) return localizer["Yesterday"];
        if (diff.TotalDays < 7) return localizer["DaysAgo", (int)diff.TotalDays];
        if (diff.TotalDays < 30) return localizer["WeeksAgo", (int)(diff.TotalDays / 7)];
        if (diff.TotalDays < 365) return localizer["MonthsAgo", (int)(diff.TotalDays / 30)];
        return localizer["YearsAgo", (int)(diff.TotalDays / 365)];
    }
}
