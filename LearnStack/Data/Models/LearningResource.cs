﻿using System.ComponentModel.DataAnnotations;

namespace LearnStack.Data.Models;

public class LearningResource
{
    public int Id { get; set; }

    [Required]
    [Url]
    [MaxLength(2000)]
    public string Url { get; set; } = string.Empty;

    [Required]
    [MaxLength(500)]
    public string Title { get; set; } = string.Empty;

    [MaxLength(2000)]
    public string? Description { get; set; }

    [Required]
    public ContentType ContentType { get; set; }

    [Required]
    public ContentStatus Status { get; set; } = ContentStatus.ToLearn;

    [Required]
    public Priority Priority { get; set; } = Priority.Medium;

    public string? Notes { get; set; }

    public DateTime DateAdded { get; set; } = DateTime.UtcNow;

    public DateTime? DateCompleted { get; set; }

    [MaxLength(1000)]
    public string? Tags { get; set; }

    [MaxLength(2000)]
    public string? ThumbnailUrl { get; set; }

    public byte[]? ThumbnailImage { get; set; }

    public int CustomOrder { get; set; }

    public bool IsArchived { get; set; }

    [Required]
    public string UserId { get; set; } = string.Empty;

    public ApplicationUser? User { get; set; }
}

