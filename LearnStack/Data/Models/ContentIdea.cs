﻿using System.ComponentModel.DataAnnotations;

namespace LearnStack.Data.Models;

public enum IdeaStatus
{
    Idea,
    InProgress,
    Published
}

public enum ContentIdeaType
{
    BlogPost,
    Video,
    TweetThread,
    Tutorial,
    Other
}

public class ContentIdea
{
    public int Id { get; set; }

    [Required]
    [MaxLength(500)]
    public string Title { get; set; } = string.Empty;

    [Required]
    public ContentIdeaType ContentType { get; set; }

    [MaxLength(2000)]
    public string? Description { get; set; }

    public string? Outline { get; set; }

    [Required]
    public IdeaStatus Status { get; set; } = IdeaStatus.Idea;

    [Required]
    public Priority Priority { get; set; } = Priority.Medium;

    public string? Notes { get; set; }

    public DateTime DateCreated { get; set; } = DateTime.UtcNow;

    public DateTime? DatePublished { get; set; }

    public int CustomOrder { get; set; }

    [Required]
    public string UserId { get; set; } = string.Empty;

    public ApplicationUser? User { get; set; }

    public ICollection<ContentIdeaResource> SourceResources { get; set; } = new List<ContentIdeaResource>();
}

