using System.Data.Common;
using System.Globalization;
using LearnStack.Core.Helpers;
using LearnStack.Data.Models;
using Microsoft.AspNetCore.Components;

namespace LearnStack.Components.Pages;

public partial class Pulse
{
    private const int WeekCount = 12;

    private List<PulseResourceState> resources = [];
    private List<PulseResourceState> recentCompletions = [];
    private IReadOnlyList<int> weeklyCompletionCounts = [];
    private IReadOnlyList<string> weekLabels = [];
    private IReadOnlyDictionary<ContentType, int> contentTypeCounts = new Dictionary<ContentType, int>();
    private bool isLoading = true;
    private bool loadInProgress;
    private bool hasLoadError;
    private int completedCount;
    private int completionRate;
    private int activeDays;
    private int queueCount;
    private int currentPeriodCompletions;
    private int previousPeriodCompletions;
    private int inProgressCount;
    private int completedWithNotesCount;

    protected override async Task OnInitializedAsync()
    {
        // Skip the prerender pass: it would load everything a second time once the circuit connects.
        if (!RendererInfo.IsInteractive)
        {
            return;
        }

        await LoadPulseAsync();
    }

    private async Task LoadPulseAsync()
    {
        if (loadInProgress)
        {
            return;
        }

        loadInProgress = true;
        isLoading = true;
        hasLoadError = false;

        try
        {
            var authState = await AuthenticationStateProvider.GetAuthenticationStateAsync();
            var user = await UserManager.GetUserAsync(authState.User);

            if (user is null)
            {
                resources = [];
            }
            else
            {
                var loadedResources = await ResourceService.GetAllAsync(user.Id);
                resources = loadedResources
                    .Where(resource => !resource.IsArchived)
                    .Select(resource => new PulseResourceState(
                        resource.Id,
                        resource.Url,
                        resource.Title,
                        resource.ContentType,
                        resource.Status,
                        !string.IsNullOrWhiteSpace(resource.Notes),
                        resource.DateAdded,
                        resource.DateCompleted))
                    .ToList();
            }

            BuildPulse(DateTime.UtcNow, user?.TimeZoneId);
        }
        catch (DbException exception)
        {
            Logger.LogError(exception, "Failed to load learning pulse data.");
            hasLoadError = true;
        }
        catch (InvalidOperationException exception)
        {
            Logger.LogError(exception, "Failed to load learning pulse data.");
            hasLoadError = true;
        }
        finally
        {
            loadInProgress = false;
            isLoading = false;
        }
    }

    private void BuildPulse(DateTime utcNow, string? timeZoneId)
    {
        var completedResources = resources
            .Where(resource => resource.Status == ContentStatus.Completed)
            .OrderByDescending(resource => resource.DateCompleted)
            .ToList();

        completedCount = completedResources.Count;
        completionRate = resources.Count == 0
            ? 0
            : (int)Math.Round(completedCount * 100d / resources.Count);
        queueCount = resources.Count(resource => resource.Status != ContentStatus.Completed);
        inProgressCount = resources.Count(resource => resource.Status == ContentStatus.InProgress);
        completedWithNotesCount = completedResources.Count(resource => resource.HasNotes);
        recentCompletions = completedResources
            .Where(resource => resource.DateCompleted.HasValue)
            .Take(3)
            .ToList();

        // Day/week boundaries are computed in the user's local time zone so that
        // "today", the trailing 30-day windows, and active-day counts line up with
        // the calendar day the user actually experienced, not the UTC calendar day.
        var timeZone = UserTimeZoneHelper.ResolveTimeZone(timeZoneId);
        var localNow = UserTimeZoneHelper.ConvertUtcToUserLocal(utcNow, timeZone);

        var currentPeriodStartLocal = localNow.Date.AddDays(-29);
        var previousPeriodStartLocal = currentPeriodStartLocal.AddDays(-30);
        var currentPeriodEndLocal = localNow.Date.AddDays(1);

        var currentPeriodStart = UserTimeZoneHelper.ConvertUserLocalToUtc(currentPeriodStartLocal, timeZone);
        var previousPeriodStart = UserTimeZoneHelper.ConvertUserLocalToUtc(previousPeriodStartLocal, timeZone);
        var currentPeriodEnd = UserTimeZoneHelper.ConvertUserLocalToUtc(currentPeriodEndLocal, timeZone);

        currentPeriodCompletions = CountCompletions(completedResources, currentPeriodStart, currentPeriodEnd);
        previousPeriodCompletions = CountCompletions(completedResources, previousPeriodStart, currentPeriodStart);
        activeDays = GetActiveDays(currentPeriodStart, currentPeriodEnd, timeZone);
        contentTypeCounts = resources
            .GroupBy(resource => resource.ContentType)
            .ToDictionary(group => group.Key, group => group.Count());

        BuildWeeklyActivity(completedResources, utcNow, timeZoneId);
    }

    private static int CountCompletions(
        IEnumerable<PulseResourceState> completedResources,
        DateTime periodStart,
        DateTime periodEnd)
    {
        return completedResources.Count(resource =>
            resource.DateCompleted >= periodStart && resource.DateCompleted < periodEnd);
    }

    private int GetActiveDays(DateTime periodStart, DateTime periodEnd, TimeZoneInfo timeZone)
    {
        return resources
            .SelectMany(resource => new DateTime?[] { resource.DateAdded, resource.DateCompleted })
            .Where(activityDate => activityDate >= periodStart && activityDate < periodEnd)
            .Select(activityDate => UserTimeZoneHelper.ConvertUtcToUserLocal(activityDate!.Value, timeZone).Date)
            .Distinct()
            .Count();
    }

    private void BuildWeeklyActivity(IReadOnlyList<PulseResourceState> completedResources, DateTime utcNow, string? timeZoneId)
    {
        var completionDatesUtc = completedResources
            .Where(resource => resource.DateCompleted.HasValue)
            .Select(resource => resource.DateCompleted!.Value)
            .ToList();

        weeklyCompletionCounts = PulseAnalyticsHelper.GetWeeklyCompletionCounts(completionDatesUtc, utcNow, timeZoneId, WeekCount);
        weekLabels = PulseAnalyticsHelper.GetWeekStartDatesLocal(utcNow, timeZoneId, WeekCount)
            .Select(weekStart => weekStart.ToString("MMM d", CultureInfo.CurrentCulture))
            .ToList();
    }

    private string GetContentTypeText(ContentType contentType) => contentType switch
    {
        ContentType.BlogPost => L["BlogPost"],
        ContentType.Podcast => L["Podcast"],
        ContentType.Video => L["Video"],
        ContentType.Article => L["Article"],
        ContentType.Course => L["Course"],
        ContentType.Documentation => L["Documentation"],
        _ => contentType.ToString()
    };

    private static string FormatCompletionDate(DateTime? dateCompleted)
    {
        return dateCompleted?.ToString("d", CultureInfo.CurrentCulture) ?? string.Empty;
    }

    private sealed record PulseResourceState(
        int Id,
        string Url,
        string Title,
        ContentType ContentType,
        ContentStatus Status,
        bool HasNotes,
        DateTime DateAdded,
        DateTime? DateCompleted);
}