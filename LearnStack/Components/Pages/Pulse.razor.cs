using System.Data.Common;
using System.Globalization;
using System.Security.Claims;
using LearnStack.Data.Models;
using Microsoft.AspNetCore.Components;

namespace LearnStack.Components.Pages;

public partial class Pulse
{
    private const string PageStateKey = "learning-pulse-page-state";
    private const int WeekCount = 12;

    private List<PulseResourceState> resources = [];
    private List<PulseResourceState> recentCompletions = [];
    private IReadOnlyList<int> weeklyCompletionCounts = [];
    private IReadOnlyList<string> weekLabels = [];
    private IReadOnlyDictionary<ContentType, int> contentTypeCounts = new Dictionary<ContentType, int>();
    private PersistingComponentStateSubscription? persistingSubscription;
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
        persistingSubscription = ApplicationState.RegisterOnPersisting(PersistPageState);

        if (ApplicationState.TryTakeFromJson<List<PulseResourceState>>(PageStateKey, out var restoredResources)
            && restoredResources is not null)
        {
            resources = restoredResources;
            BuildPulse(DateTime.UtcNow);
            isLoading = false;
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
            var userId = authState.User.FindFirst(ClaimTypes.NameIdentifier)?.Value;

            if (string.IsNullOrWhiteSpace(userId))
            {
                resources = [];
            }
            else
            {
                var loadedResources = await ResourceService.GetAllAsync(userId);
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

            BuildPulse(DateTime.UtcNow);
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

    private void BuildPulse(DateTime utcNow)
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

        var currentPeriodStart = utcNow.Date.AddDays(-29);
        var previousPeriodStart = currentPeriodStart.AddDays(-30);
        var currentPeriodEnd = utcNow.Date.AddDays(1);

        currentPeriodCompletions = CountCompletions(completedResources, currentPeriodStart, currentPeriodEnd);
        previousPeriodCompletions = CountCompletions(completedResources, previousPeriodStart, currentPeriodStart);
        activeDays = GetActiveDays(currentPeriodStart, currentPeriodEnd);
        contentTypeCounts = resources
            .GroupBy(resource => resource.ContentType)
            .ToDictionary(group => group.Key, group => group.Count());

        BuildWeeklyActivity(completedResources, utcNow);
    }

    private static int CountCompletions(
        IEnumerable<PulseResourceState> completedResources,
        DateTime periodStart,
        DateTime periodEnd)
    {
        return completedResources.Count(resource =>
            resource.DateCompleted >= periodStart && resource.DateCompleted < periodEnd);
    }

    private int GetActiveDays(DateTime periodStart, DateTime periodEnd)
    {
        return resources
            .SelectMany(resource => new DateTime?[] { resource.DateAdded, resource.DateCompleted })
            .Where(activityDate => activityDate >= periodStart && activityDate < periodEnd)
            .Select(activityDate => activityDate!.Value.Date)
            .Distinct()
            .Count();
    }

    private void BuildWeeklyActivity(IReadOnlyList<PulseResourceState> completedResources, DateTime utcNow)
    {
        var daysSinceMonday = ((int)utcNow.DayOfWeek + 6) % 7;
        var currentWeekStart = utcNow.Date.AddDays(-daysSinceMonday);
        var firstWeekStart = currentWeekStart.AddDays(-7 * (WeekCount - 1));
        var counts = new List<int>(WeekCount);
        var labels = new List<string>(WeekCount);

        for (var index = 0; index < WeekCount; index++)
        {
            var weekStart = firstWeekStart.AddDays(index * 7);
            var weekEnd = weekStart.AddDays(7);
            counts.Add(CountCompletions(completedResources, weekStart, weekEnd));
            labels.Add(weekStart.ToString("MMM d", CultureInfo.CurrentCulture));
        }

        weeklyCompletionCounts = counts;
        weekLabels = labels;
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

    private Task PersistPageState()
    {
        ApplicationState.PersistAsJson(PageStateKey, resources);
        return Task.CompletedTask;
    }

    public void Dispose()
    {
        persistingSubscription?.Dispose();
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