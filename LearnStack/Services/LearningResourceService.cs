﻿using LearnStack.Data;
using LearnStack.Data.Models;
using Microsoft.EntityFrameworkCore;

namespace LearnStack.Services;

public class LearningResourceService : ILearningResourceService
{
    private readonly IDbContextFactory<ApplicationDbContext> _contextFactory;

    public LearningResourceService(IDbContextFactory<ApplicationDbContext> contextFactory)
    {
        _contextFactory = contextFactory;
    }

    public async Task<List<LearningResource>> GetAllAsync(string userId)
    {
        await using var context = await _contextFactory.CreateDbContextAsync();
        return await context.LearningResources
            .Where(lr => lr.UserId == userId)
            .OrderByDescending(lr => lr.Priority)
            .ThenBy(lr => lr.CustomOrder)
            .ThenByDescending(lr => lr.DateAdded)
            .ToListAsync();
    }

    public async Task<bool> ToggleArchiveAsync(int id, string userId)
    {
        await using var context = await _contextFactory.CreateDbContextAsync();
        var resource = await context.LearningResources
            .FirstOrDefaultAsync(lr => lr.Id == id && lr.UserId == userId);
        if (resource == null) return false;

        resource.IsArchived = !resource.IsArchived;
        await context.SaveChangesAsync();
        return resource.IsArchived;
    }

    public async Task ArchiveStaleResourcesAsync(string userId)
    {
        await using var context = await _contextFactory.CreateDbContextAsync();
        var cutoff = DateTime.UtcNow.AddDays(-180);
        var staleResources = await context.LearningResources
            .Where(lr => lr.UserId == userId
                      && lr.Status == ContentStatus.ToLearn
                      && !lr.IsArchived
                      && lr.DateAdded <= cutoff)
            .ToListAsync();

        if (staleResources.Count == 0) return;

        foreach (var resource in staleResources)
        {
            resource.IsArchived = true;
        }

        await context.SaveChangesAsync();
    }

    public async Task<List<LearningResource>> GetByStatusAsync(string userId, ContentStatus status)
    {
        await using var context = await _contextFactory.CreateDbContextAsync();
        return await context.LearningResources
            .Where(lr => lr.UserId == userId && lr.Status == status)
            .OrderByDescending(lr => lr.Priority)
            .ThenBy(lr => lr.CustomOrder)
            .ThenByDescending(lr => lr.DateAdded)
            .ToListAsync();
    }

    public async Task<LearningResource?> GetByIdAsync(int id, string userId)
    {
        await using var context = await _contextFactory.CreateDbContextAsync();
        return await context.LearningResources
            .FirstOrDefaultAsync(lr => lr.Id == id && lr.UserId == userId);
    }

    public async Task<bool> UrlExistsAsync(string userId, string url, int? excludeResourceId = null)
    {
        await using var context = await _contextFactory.CreateDbContextAsync();
        var normalizedUrl = NormalizeUrl(url);
        if (string.IsNullOrWhiteSpace(normalizedUrl))
        {
            return false;
        }

        var query = context.LearningResources
            .Where(lr => lr.UserId == userId);

        if (excludeResourceId.HasValue)
        {
            query = query.Where(lr => lr.Id != excludeResourceId.Value);
        }

        var existingUrls = await query
            .Select(lr => lr.Url)
            .ToListAsync();

        return existingUrls.Any(existingUrl =>
            string.Equals(NormalizeUrl(existingUrl), normalizedUrl, StringComparison.OrdinalIgnoreCase));
    }

    public async Task<LearningResource> CreateAsync(LearningResource resource)
    {
        await using var context = await _contextFactory.CreateDbContextAsync();
        context.LearningResources.Add(resource);
        await context.SaveChangesAsync();
        return resource;
    }

    public async Task<LearningResource?> UpdateAsync(LearningResource resource, string userId)
    {
        await using var context = await _contextFactory.CreateDbContextAsync();
        // Verify ownership before updating
        var existing = await context.LearningResources
            .FirstOrDefaultAsync(lr => lr.Id == resource.Id && lr.UserId == userId);
        if (existing == null) return null;
        
        existing.Url = resource.Url;
        existing.Title = resource.Title;
        existing.Description = resource.Description;
        existing.ContentType = resource.ContentType;
        existing.Status = resource.Status;
        existing.Priority = resource.Priority;
        existing.Notes = resource.Notes;
        existing.Tags = resource.Tags;
        existing.ThumbnailUrl = resource.ThumbnailUrl;
        existing.ThumbnailImage = resource.ThumbnailImage;
        existing.CustomOrder = resource.CustomOrder;
        existing.DateCompleted = resource.DateCompleted;
        existing.IsArchived = resource.IsArchived;
        existing.IsPublic = resource.IsPublic;
        
        await context.SaveChangesAsync();
        return existing;
    }

    public async Task DeleteAsync(int id, string userId)
    {
        await using var context = await _contextFactory.CreateDbContextAsync();
        var resource = await context.LearningResources
            .FirstOrDefaultAsync(lr => lr.Id == id && lr.UserId == userId);
        if (resource != null)
        {
            context.LearningResources.Remove(resource);
            await context.SaveChangesAsync();
        }
    }

    public async Task<List<LearningResource>> SearchAsync(string userId, string searchTerm)
    {
        await using var context = await _contextFactory.CreateDbContextAsync();
        var term = searchTerm.ToLower();
        return await context.LearningResources
            .Where(lr => lr.UserId == userId &&
                        (lr.Title.ToLower().Contains(term) ||
                         (lr.Description != null && lr.Description.ToLower().Contains(term)) ||
                         (lr.Notes != null && lr.Notes.ToLower().Contains(term)) ||
                         (lr.Tags != null && lr.Tags.ToLower().Contains(term))))
            .OrderByDescending(lr => lr.Priority)
            .ThenBy(lr => lr.CustomOrder)
            .ThenByDescending(lr => lr.DateAdded)
            .ToListAsync();
    }

    public async Task UpdateOrderAsync(string userId, List<int> orderedIds)
    {
        await using var context = await _contextFactory.CreateDbContextAsync();
        for (int i = 0; i < orderedIds.Count; i++)
        {
            var resource = await context.LearningResources
                .FirstOrDefaultAsync(lr => lr.Id == orderedIds[i] && lr.UserId == userId);
            if (resource != null)
            {
                resource.CustomOrder = i;
            }
        }
        await context.SaveChangesAsync();
    }

    public async Task<bool> TogglePublicAsync(int id, string userId)
    {
        await using var context = await _contextFactory.CreateDbContextAsync();
        var resource = await context.LearningResources
            .FirstOrDefaultAsync(lr => lr.Id == id && lr.UserId == userId);
        if (resource == null) return false;

        resource.IsPublic = !resource.IsPublic;
        await context.SaveChangesAsync();
        return resource.IsPublic;
    }

    public async Task<List<LearningResource>> GetPublicResourcesByUserIdAsync(string ownerUserId)
    {
        await using var context = await _contextFactory.CreateDbContextAsync();
        return await context.LearningResources
            .Where(lr => lr.UserId == ownerUserId && lr.IsPublic && !lr.IsArchived)
            .OrderByDescending(lr => lr.Priority)
            .ThenBy(lr => lr.CustomOrder)
            .ThenByDescending(lr => lr.DateAdded)
            .ToListAsync();
    }

    private static string NormalizeUrl(string url)    {
        if (string.IsNullOrWhiteSpace(url))
        {
            return string.Empty;
        }

        var trimmedUrl = url.Trim();

        if (Uri.TryCreate(trimmedUrl, UriKind.Absolute, out var uri))
        {
            var builder = new UriBuilder(uri)
            {
                Host = uri.Host.ToLowerInvariant(),
                Scheme = uri.Scheme.ToLowerInvariant()
            };

            return builder.Uri.AbsoluteUri.TrimEnd('/');
        }

        return trimmedUrl.TrimEnd('/').ToLowerInvariant();
    }
}

