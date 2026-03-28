using LearnStack.Data;
using LearnStack.Data.Models;
using Microsoft.EntityFrameworkCore;

namespace LearnStack.Services;

public class ContentIdeaService : IContentIdeaService
{
    private readonly IDbContextFactory<ApplicationDbContext> _contextFactory;

    public ContentIdeaService(IDbContextFactory<ApplicationDbContext> contextFactory)
    {
        _contextFactory = contextFactory;
    }

    public async Task<List<ContentIdea>> GetAllAsync(string userId)
    {
        await using var context = await _contextFactory.CreateDbContextAsync();
        return await context.ContentIdeas
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
        await using var context = await _contextFactory.CreateDbContextAsync();
        return await context.ContentIdeas
            .Include(ci => ci.SourceResources)
                .ThenInclude(cir => cir.LearningResource)
            .FirstOrDefaultAsync(ci => ci.Id == id && ci.UserId == userId);
    }

    public async Task<ContentIdea> CreateAsync(ContentIdea idea)
    {
        await using var context = await _contextFactory.CreateDbContextAsync();
        context.ContentIdeas.Add(idea);
        await context.SaveChangesAsync();
        return idea;
    }

    public async Task<ContentIdea?> UpdateAsync(ContentIdea idea, string userId)
    {
        await using var context = await _contextFactory.CreateDbContextAsync();
        // Verify ownership before updating
        var existing = await context.ContentIdeas
            .Include(ci => ci.SourceResources)
                .ThenInclude(cir => cir.LearningResource)
            .FirstOrDefaultAsync(ci => ci.Id == idea.Id && ci.UserId == userId);
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
        
        await context.SaveChangesAsync();
        return existing;
    }

    public async Task DeleteAsync(int id, string userId)
    {
        await using var context = await _contextFactory.CreateDbContextAsync();
        var idea = await context.ContentIdeas
            .FirstOrDefaultAsync(ci => ci.Id == id && ci.UserId == userId);
        if (idea != null)
        {
            context.ContentIdeas.Remove(idea);
            await context.SaveChangesAsync();
        }
    }

    public async Task AddSourceResourceAsync(int ideaId, int resourceId, string userId)
    {
        await using var context = await _contextFactory.CreateDbContextAsync();
        // Verify idea belongs to user
        var idea = await context.ContentIdeas
            .FirstOrDefaultAsync(ci => ci.Id == ideaId && ci.UserId == userId);
        if (idea == null) return;
        
        // Verify resource belongs to user
        var resource = await context.LearningResources
            .FirstOrDefaultAsync(lr => lr.Id == resourceId && lr.UserId == userId);
        if (resource == null) return;
        
        // Check if link already exists
        var existingLink = await context.ContentIdeaResources
            .FirstOrDefaultAsync(cir => cir.ContentIdeaId == ideaId && cir.LearningResourceId == resourceId);
        if (existingLink != null) return;
        
        var link = new ContentIdeaResource
        {
            ContentIdeaId = ideaId,
            LearningResourceId = resourceId
        };
        context.ContentIdeaResources.Add(link);
        await context.SaveChangesAsync();
    }

    public async Task RemoveSourceResourceAsync(int ideaId, int resourceId, string userId)
    {
        await using var context = await _contextFactory.CreateDbContextAsync();
        // Verify idea belongs to user first
        var idea = await context.ContentIdeas
            .FirstOrDefaultAsync(ci => ci.Id == ideaId && ci.UserId == userId);
        if (idea == null) return;
        
        var link = await context.ContentIdeaResources
            .FirstOrDefaultAsync(cir => cir.ContentIdeaId == ideaId && cir.LearningResourceId == resourceId);
        
        if (link != null)
        {
            context.ContentIdeaResources.Remove(link);
            await context.SaveChangesAsync();
        }
    }

    public async Task UpdateOrderAsync(string userId, List<int> orderedIds)
    {
        await using var context = await _contextFactory.CreateDbContextAsync();

        var ideasById = await context.ContentIdeas
            .Where(ci => ci.UserId == userId && orderedIds.Contains(ci.Id))
            .ToDictionaryAsync(ci => ci.Id);

        for (int i = 0; i < orderedIds.Count; i++)
        {
            if (ideasById.TryGetValue(orderedIds[i], out var idea))
            {
                idea.CustomOrder = i;
            }
        }

        await context.SaveChangesAsync();
    }
}


