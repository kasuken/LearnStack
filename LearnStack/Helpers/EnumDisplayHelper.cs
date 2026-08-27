using LearnStack.Data.Models;
using MudBlazor;

namespace LearnStack.Helpers;

public static class EnumDisplayHelper
{
    // ── ContentType ───────────────────────────────────────────────────────────

    public static Color GetColor(ContentType type) => type switch
    {
        ContentType.BlogPost      => Color.Primary,
        ContentType.Podcast       => Color.Secondary,
        ContentType.Video         => Color.Error,
        ContentType.Article       => Color.Info,
        ContentType.Course        => Color.Warning,
        ContentType.Documentation => Color.Success,
        _                         => Color.Default
    };

    public static string GetIcon(ContentType type) => type switch
    {
        ContentType.BlogPost      => Icons.Material.Filled.Article,
        ContentType.Podcast       => Icons.Material.Filled.Podcasts,
        ContentType.Video         => Icons.Material.Filled.VideoLibrary,
        ContentType.Article       => Icons.Material.Filled.Description,
        ContentType.Course        => Icons.Material.Filled.School,
        ContentType.Documentation => Icons.Material.Filled.MenuBook,
        _                         => Icons.Material.Filled.Link
    };

    public static string GetChipClass(ContentType contentType) => contentType switch
    {
        ContentType.Video         => "chip-type-video",
        ContentType.Article       => "chip-type-article",
        ContentType.Course        => "chip-type-course",
        ContentType.Podcast       => "chip-type-podcast",
        ContentType.Documentation => "chip-type-documentation",
        _                         => "chip-type-default"
    };

    public static string GetChipStyle(ContentType contentType) => contentType switch
    {
        ContentType.Video         => "background: #ffebee; color: #c62828; font-weight: 500;",
        ContentType.Article       => "background: #e3f2fd; color: #1565c0; font-weight: 500;",
        ContentType.Course        => "background: #fff3e0; color: #e65100; font-weight: 500;",
        ContentType.Podcast       => "background: #f3e8ff; color: #7b1fa2; font-weight: 500;",
        ContentType.Documentation => "background: #e8f5e9; color: #2e7d32; font-weight: 500;",
        _                         => "background: #f5f5f5; color: #616161; font-weight: 500;"
    };

    // ── ContentStatus ─────────────────────────────────────────────────────────

    public static Color GetColor(ContentStatus status) => status switch
    {
        ContentStatus.ToLearn    => Color.Info,
        ContentStatus.InProgress => Color.Warning,
        ContentStatus.Completed  => Color.Success,
        _                        => Color.Default
    };

    public static string GetChipClass(ContentStatus status) => status switch
    {
        ContentStatus.ToLearn    => "chip-status-idea",
        ContentStatus.InProgress => "chip-status-progress",
        ContentStatus.Completed  => "chip-status-complete",
        _                        => "chip-type-default"
    };

    public static string GetChipStyle(ContentStatus status) => status switch
    {
        ContentStatus.ToLearn    => "background: #e3f2fd; color: #1565c0; font-weight: 500;",
        ContentStatus.InProgress => "background: #fff3e0; color: #e65100; font-weight: 500;",
        ContentStatus.Completed  => "background: #e8f5e9; color: #2e7d32; font-weight: 500;",
        _                        => "background: #f5f5f5; color: #616161; font-weight: 500;"
    };

    // ── Priority ──────────────────────────────────────────────────────────────

    public static Color GetColor(Priority priority) => priority switch
    {
        Priority.High   => Color.Error,
        Priority.Medium => Color.Warning,
        Priority.Low    => Color.Default,
        _               => Color.Default
    };

    public static string GetPriorityStyle(Priority priority) => priority switch
    {
        Priority.High   => "background-color: #ffebee; color: #c62828;",
        Priority.Medium => "background-color: #fff8e1; color: #f57f17;",
        Priority.Low    => "background-color: #f5f5f5; color: #757575;",
        _               => ""
    };

    public static string GetChipClass(Priority priority) => priority switch
    {
        Priority.High   => "chip-priority-high",
        Priority.Medium => "chip-priority-medium",
        Priority.Low    => "chip-priority-low",
        _               => "chip-priority-low"
    };

    public static string GetChipStyle(Priority priority) => priority switch
    {
        Priority.High   => "background: #ffebee; color: #c62828; font-weight: 500;",
        Priority.Medium => "background: #fff3e0; color: #e65100; font-weight: 500;",
        Priority.Low    => "background: #f5f5f5; color: #616161; font-weight: 500;",
        _               => "background: #f5f5f5; color: #616161; font-weight: 500;"
    };

    // ── ContentIdeaType ───────────────────────────────────────────────────────

    public static Color GetColor(ContentIdeaType type) => type switch
    {
        ContentIdeaType.BlogPost    => Color.Primary,
        ContentIdeaType.Video       => Color.Error,
        ContentIdeaType.TweetThread => Color.Info,
        ContentIdeaType.Tutorial    => Color.Warning,
        ContentIdeaType.Other       => Color.Secondary,
        _                           => Color.Default
    };

    public static string GetChipClass(ContentIdeaType contentType) => contentType switch
    {
        ContentIdeaType.Video       => "chip-idea-video",
        ContentIdeaType.BlogPost    => "chip-idea-blogpost",
        ContentIdeaType.Tutorial    => "chip-idea-tutorial",
        ContentIdeaType.TweetThread => "chip-idea-tweetthread",
        ContentIdeaType.Other       => "chip-idea-other",
        _                           => "chip-type-default"
    };

    public static string GetChipStyle(ContentIdeaType contentType) => contentType switch
    {
        ContentIdeaType.Video       => "background: #ffebee; color: #c62828; font-weight: 500;",
        ContentIdeaType.BlogPost    => "background: #e3f2fd; color: #1565c0; font-weight: 500;",
        ContentIdeaType.Tutorial    => "background: #fff3e0; color: #e65100; font-weight: 500;",
        ContentIdeaType.TweetThread => "background: #f3e8ff; color: #7b1fa2; font-weight: 500;",
        ContentIdeaType.Other       => "background: #e8f5e9; color: #2e7d32; font-weight: 500;",
        _                           => "background: #f5f5f5; color: #616161; font-weight: 500;"
    };

    // ── IdeaStatus ────────────────────────────────────────────────────────────

    public static Color GetColor(IdeaStatus status) => status switch
    {
        IdeaStatus.Idea       => Color.Info,
        IdeaStatus.InProgress => Color.Warning,
        IdeaStatus.Published  => Color.Success,
        _                     => Color.Default
    };

    public static string GetChipClass(IdeaStatus status) => status switch
    {
        IdeaStatus.Idea       => "chip-status-idea",
        IdeaStatus.InProgress => "chip-status-progress",
        IdeaStatus.Published  => "chip-status-complete",
        _                     => "chip-type-default"
    };

    public static string GetChipStyle(IdeaStatus status) => status switch
    {
        IdeaStatus.Idea       => "background: #e3f2fd; color: #1565c0; font-weight: 500;",
        IdeaStatus.InProgress => "background: #fff3e0; color: #e65100; font-weight: 500;",
        IdeaStatus.Published  => "background: #e8f5e9; color: #2e7d32; font-weight: 500;",
        _                     => "background: #f5f5f5; color: #616161; font-weight: 500;"
    };
}
