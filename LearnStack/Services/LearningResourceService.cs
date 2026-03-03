﻿using LearnStack.Data;
using LearnStack.Data.Models;
using Microsoft.EntityFrameworkCore;

namespace LearnStack.Services;

public class LearningResourceService : ILearningResourceService
{
    private readonly ApplicationDbContext _context;

    public LearningResourceService(ApplicationDbContext context)
    {
        _context = context;
    }

    public async Task<List<LearningResource>> GetAllAsync(string userId)
    {
        return await _context.LearningResources
            .Where(lr => lr.UserId == userId)
            .OrderByDescending(lr => lr.Priority)
            .ThenBy(lr => lr.CustomOrder)
            .ThenByDescending(lr => lr.DateAdded)
            .ToListAsync();
    }

    public async Task<bool> ToggleArchiveAsync(int id, string userId)
    {
        var resource = await GetByIdAsync(id, userId);
        if (resource == null) return false;

        resource.IsArchived = !resource.IsArchived;
        await _context.SaveChangesAsync();
        return resource.IsArchived;
    }

    public async Task ArchiveStaleResourcesAsync(string userId)
    {
        var cutoff = DateTime.UtcNow.AddDays(-180);
        var staleResources = await _context.LearningResources
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

        await _context.SaveChangesAsync();
    }

    public async Task<List<LearningResource>> GetByStatusAsync(string userId, ContentStatus status)
    {
        return await _context.LearningResources
            .Where(lr => lr.UserId == userId && lr.Status == status)
            .OrderByDescending(lr => lr.Priority)
            .ThenBy(lr => lr.CustomOrder)
            .ThenByDescending(lr => lr.DateAdded)
            .ToListAsync();
    }

    public async Task<LearningResource?> GetByIdAsync(int id, string userId)
    {
        return await _context.LearningResources
            .FirstOrDefaultAsync(lr => lr.Id == id && lr.UserId == userId);
    }

    public async Task<bool> UrlExistsAsync(string userId, string url, int? excludeResourceId = null)
    {
        var normalizedUrl = NormalizeUrl(url);
        if (string.IsNullOrWhiteSpace(normalizedUrl))
        {
            return false;
        }

        var query = _context.LearningResources
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
        _context.LearningResources.Add(resource);
        await _context.SaveChangesAsync();
        return resource;
    }

    public async Task<LearningResource?> UpdateAsync(LearningResource resource, string userId)
    {
        // Verify ownership before updating
        var existing = await GetByIdAsync(resource.Id, userId);
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
        
        await _context.SaveChangesAsync();
        return existing;
    }

    public async Task DeleteAsync(int id, string userId)
    {
        var resource = await GetByIdAsync(id, userId);
        if (resource != null)
        {
            _context.LearningResources.Remove(resource);
            await _context.SaveChangesAsync();
        }
    }

    public async Task<List<LearningResource>> SearchAsync(string userId, string searchTerm)
    {
        var term = searchTerm.ToLower();
        return await _context.LearningResources
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
        for (int i = 0; i < orderedIds.Count; i++)
        {
            var resource = await GetByIdAsync(orderedIds[i], userId);
            if (resource != null)
            {
                resource.CustomOrder = i;
            }
        }
        await _context.SaveChangesAsync();
    }

    private static string NormalizeUrl(string url)
    {
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

