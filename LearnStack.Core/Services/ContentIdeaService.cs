using LearnStack.Data;
using LearnStack.Data.Models;
using Microsoft.EntityFrameworkCore;

namespace LearnStack.Services;

public class ContentIdeaService(IDbContextFactory<ApplicationDbContext> contextFactory) : IContentIdeaService
{
    private readonly IDbContextFactory<ApplicationDbContext> _contextFactory =
        contextFactory ?? throw new ArgumentNullException(nameof(contextFactory));

    public async Task<List<ContentIdea>> GetAllAsync(string userId)
    {
        await using var context = await _contextFactory.CreateDbContextAsync();
        return await QueryIdeasWithSourceResources(context)
            .AsNoTracking()
            .Where(ci => ci.UserId == userId)
            .OrderByDescending(ci => ci.Priority)
            .ThenBy(ci => ci.CustomOrder)
            .ThenByDescending(ci => ci.DateCreated)
            .ToListAsync();
    }

    public async Task<ContentIdea?> GetByIdAsync(int id, string userId)
    {
        await using var context = await _contextFactory.CreateDbContextAsync();
        return await QueryIdeasWithSourceResources(context)
            .AsNoTracking()
            .FirstOrDefaultAsync(ci => ci.Id == id && ci.UserId == userId);
    }

    public async Task<ContentIdea> CreateAsync(ContentIdea idea)
    {
        ArgumentNullException.ThrowIfNull(idea);

        await using var context = await _contextFactory.CreateDbContextAsync();
        context.ContentIdeas.Add(idea);
        await context.SaveChangesAsync();
        return idea;
    }

    public async Task<ContentIdea?> UpdateAsync(ContentIdea idea, string userId)
    {
        ArgumentNullException.ThrowIfNull(idea);

        await using var context = await _contextFactory.CreateDbContextAsync();
        var existing = await QueryOwnedIdeas(context, userId)
            .FirstOrDefaultAsync(ci => ci.Id == idea.Id);
        if (existing == null) return null;

        ApplyUpdates(existing, idea);

        await context.SaveChangesAsync();
        return existing;
    }

    public async Task<bool> DeleteAsync(int id, string userId)
    {
        await using var context = await _contextFactory.CreateDbContextAsync();
        var idea = await QueryOwnedIdeas(context, userId)
            .FirstOrDefaultAsync(ci => ci.Id == id);
        if (idea == null) return false;

        context.ContentIdeas.Remove(idea);
        await context.SaveChangesAsync();
        return true;
    }

    public async Task AddSourceResourceAsync(int ideaId, int resourceId, string userId)
    {
        await using var context = await _contextFactory.CreateDbContextAsync();
        if (!await IdeaExistsAsync(context, ideaId, userId)
            || !await ResourceExistsAsync(context, resourceId, userId))
        {
            return;
        }

        var linkExists = await context.ContentIdeaResources
            .AnyAsync(cir => cir.ContentIdeaId == ideaId && cir.LearningResourceId == resourceId);
        if (linkExists) return;

        context.ContentIdeaResources.Add(new ContentIdeaResource
        {
            ContentIdeaId = ideaId,
            LearningResourceId = resourceId
        });

        await context.SaveChangesAsync();
    }

    public async Task RemoveSourceResourceAsync(int ideaId, int resourceId, string userId)
    {
        await using var context = await _contextFactory.CreateDbContextAsync();
        if (!await IdeaExistsAsync(context, ideaId, userId)
            || !await ResourceExistsAsync(context, resourceId, userId))
        {
            return;
        }

        var link = await context.ContentIdeaResources
            .FirstOrDefaultAsync(cir => cir.ContentIdeaId == ideaId && cir.LearningResourceId == resourceId);
        if (link == null) return;

        context.ContentIdeaResources.Remove(link);
        await context.SaveChangesAsync();
    }

    public async Task UpdateOrderAsync(string userId, List<int> orderedIds)
    {
        ArgumentNullException.ThrowIfNull(orderedIds);

        if (orderedIds.Count == 0)
        {
            return;
        }

        await using var context = await _contextFactory.CreateDbContextAsync();

        var ideasById = await QueryOwnedIdeas(context, userId)
            .Where(ci => orderedIds.Contains(ci.Id))
            .ToDictionaryAsync(ci => ci.Id);

        for (var i = 0; i < orderedIds.Count; i++)
        {
            if (ideasById.TryGetValue(orderedIds[i], out var idea))
            {
                idea.CustomOrder = i;
            }
        }

        await context.SaveChangesAsync();
    }

    private static IQueryable<ContentIdea> QueryIdeasWithSourceResources(ApplicationDbContext context) =>
        context.ContentIdeas
            .Include(ci => ci.SourceResources)
            .ThenInclude(cir => cir.LearningResource);

    private static IQueryable<ContentIdea> QueryOwnedIdeas(ApplicationDbContext context, string userId) =>
        context.ContentIdeas.Where(ci => ci.UserId == userId);

    private static Task<bool> IdeaExistsAsync(ApplicationDbContext context, int ideaId, string userId) =>
        QueryOwnedIdeas(context, userId).AnyAsync(ci => ci.Id == ideaId);

    private static Task<bool> ResourceExistsAsync(ApplicationDbContext context, int resourceId, string userId) =>
        context.LearningResources.AnyAsync(lr => lr.Id == resourceId && lr.UserId == userId);

    private static void ApplyUpdates(ContentIdea target, ContentIdea source)
    {
        target.Title = source.Title;
        target.ContentType = source.ContentType;
        target.Description = source.Description;
        target.Outline = source.Outline;
        target.Status = source.Status;
        target.Priority = source.Priority;
        target.Notes = source.Notes;
        target.DatePublished = source.DatePublished;
        target.CustomOrder = source.CustomOrder;
    }
}
