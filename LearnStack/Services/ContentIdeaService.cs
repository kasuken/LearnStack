﻿using LearnStack.Data;
using LearnStack.Data.Models;
using Microsoft.EntityFrameworkCore;

namespace LearnStack.Services;

public class ContentIdeaService : IContentIdeaService
{
    private readonly ApplicationDbContext _context;

    public ContentIdeaService(ApplicationDbContext context)
    {
        _context = context;
    }

    public async Task<List<ContentIdea>> GetAllAsync(string userId)
    {
        return await _context.ContentIdeas
            .Include(ci => ci.SourceResources)
                .ThenInclude(cir => cir.LearningResource)
            .Where(ci => ci.UserId == userId)
            .OrderByDescending(ci => ci.Priority)
            .ThenBy(ci => ci.CustomOrder)
            .ThenByDescending(ci => ci.DateCreated)
            .ToListAsync();
    }

    public async Task<ContentIdea?> GetByIdAsync(int id, string userId)
    {
        return await _context.ContentIdeas
            .Include(ci => ci.SourceResources)
                .ThenInclude(cir => cir.LearningResource)
            .FirstOrDefaultAsync(ci => ci.Id == id && ci.UserId == userId);
    }

    public async Task<ContentIdea> CreateAsync(ContentIdea idea)
    {
        _context.ContentIdeas.Add(idea);
        await _context.SaveChangesAsync();
        return idea;
    }

    public async Task<ContentIdea?> UpdateAsync(ContentIdea idea, string userId)
    {
        // Verify ownership before updating
        var existing = await GetByIdAsync(idea.Id, userId);
        if (existing == null) return null;
        
        existing.Title = idea.Title;
        existing.ContentType = idea.ContentType;
        existing.Description = idea.Description;
        existing.Outline = idea.Outline;
        existing.Status = idea.Status;
        existing.Priority = idea.Priority;
        existing.Notes = idea.Notes;
        existing.DatePublished = idea.DatePublished;
        existing.CustomOrder = idea.CustomOrder;
        
        await _context.SaveChangesAsync();
        return existing;
    }

    public async Task DeleteAsync(int id, string userId)
    {
        var idea = await GetByIdAsync(id, userId);
        if (idea != null)
        {
            _context.ContentIdeas.Remove(idea);
            await _context.SaveChangesAsync();
        }
    }

    public async Task AddSourceResourceAsync(int ideaId, int resourceId, string userId)
    {
        // Verify idea belongs to user
        var idea = await GetByIdAsync(ideaId, userId);
        if (idea == null) return;
        
        // Verify resource belongs to user
        var resource = await _context.LearningResources
            .FirstOrDefaultAsync(lr => lr.Id == resourceId && lr.UserId == userId);
        if (resource == null) return;
        
        // Check if link already exists
        var existingLink = await _context.ContentIdeaResources
            .FirstOrDefaultAsync(cir => cir.ContentIdeaId == ideaId && cir.LearningResourceId == resourceId);
        if (existingLink != null) return;
        
        var link = new ContentIdeaResource
        {
            ContentIdeaId = ideaId,
            LearningResourceId = resourceId
        };
        _context.ContentIdeaResources.Add(link);
        await _context.SaveChangesAsync();
    }

    public async Task RemoveSourceResourceAsync(int ideaId, int resourceId, string userId)
    {
        // Verify idea belongs to user first
        var idea = await GetByIdAsync(ideaId, userId);
        if (idea == null) return;
        
        var link = await _context.ContentIdeaResources
            .FirstOrDefaultAsync(cir => cir.ContentIdeaId == ideaId && cir.LearningResourceId == resourceId);
        
        if (link != null)
        {
            _context.ContentIdeaResources.Remove(link);
            await _context.SaveChangesAsync();
        }
    }

    public async Task UpdateOrderAsync(string userId, List<int> orderedIds)
    {
        for (int i = 0; i < orderedIds.Count; i++)
        {
            var idea = await GetByIdAsync(orderedIds[i], userId);
            if (idea != null)
            {
                idea.CustomOrder = i;
            }
        }
        await _context.SaveChangesAsync();
    }
}

