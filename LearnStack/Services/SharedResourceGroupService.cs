using LearnStack.Data;
using LearnStack.Data.Models;
using Microsoft.EntityFrameworkCore;

namespace LearnStack.Services;

public class SharedResourceGroupService : ISharedResourceGroupService
{
    private readonly IDbContextFactory<ApplicationDbContext> _contextFactory;

    public SharedResourceGroupService(IDbContextFactory<ApplicationDbContext> contextFactory)
    {
        _contextFactory = contextFactory;
    }

    public async Task<List<SharedResourceGroup>> GetAllAsync(string userId)
    {
        await using var context = await _contextFactory.CreateDbContextAsync();
        return await context.SharedResourceGroups
            .Include(sg => sg.Items)
                .ThenInclude(sgi => sgi.LearningResource)
            .Where(sg => sg.UserId == userId)
            .OrderByDescending(sg => sg.DateCreated)
            .ToListAsync();
    }

    public async Task<SharedResourceGroup?> GetByIdAsync(int id, string userId)
    {
        await using var context = await _contextFactory.CreateDbContextAsync();
        return await context.SharedResourceGroups
            .Include(sg => sg.Items)
                .ThenInclude(sgi => sgi.LearningResource)
            .FirstOrDefaultAsync(sg => sg.Id == id && sg.UserId == userId);
    }

    public async Task<SharedResourceGroup?> GetByShareTokenAsync(string shareToken)
    {
        await using var context = await _contextFactory.CreateDbContextAsync();
        return await context.SharedResourceGroups
            .Include(sg => sg.Items)
                .ThenInclude(sgi => sgi.LearningResource)
            .Include(sg => sg.User)
            .FirstOrDefaultAsync(sg => sg.ShareToken == shareToken && sg.IsActive);
    }

    public async Task<SharedResourceGroup> CreateAsync(SharedResourceGroup group)
    {
        await using var context = await _contextFactory.CreateDbContextAsync();
        context.SharedResourceGroups.Add(group);
        await context.SaveChangesAsync();
        return group;
    }

    public async Task<SharedResourceGroup?> UpdateAsync(SharedResourceGroup group, string userId)
    {
        await using var context = await _contextFactory.CreateDbContextAsync();
        var existing = await context.SharedResourceGroups
            .FirstOrDefaultAsync(sg => sg.Id == group.Id && sg.UserId == userId);
        if (existing == null) return null;

        existing.Name = group.Name;
        existing.Description = group.Description;
        existing.IsActive = group.IsActive;

        await context.SaveChangesAsync();
        return existing;
    }

    public async Task DeleteAsync(int id, string userId)
    {
        await using var context = await _contextFactory.CreateDbContextAsync();
        var group = await context.SharedResourceGroups
            .FirstOrDefaultAsync(sg => sg.Id == id && sg.UserId == userId);
        if (group != null)
        {
            context.SharedResourceGroups.Remove(group);
            await context.SaveChangesAsync();
        }
    }

    public async Task AddResourceAsync(int groupId, int resourceId, string userId)
    {
        await using var context = await _contextFactory.CreateDbContextAsync();
        // Verify group belongs to user
        var group = await context.SharedResourceGroups
            .FirstOrDefaultAsync(sg => sg.Id == groupId && sg.UserId == userId);
        if (group == null) return;

        // Verify resource belongs to user
        var resource = await context.LearningResources
            .FirstOrDefaultAsync(lr => lr.Id == resourceId && lr.UserId == userId);
        if (resource == null) return;

        // Check if link already exists
        var existingLink = await context.SharedResourceGroupItems
            .FirstOrDefaultAsync(sgi => sgi.SharedResourceGroupId == groupId && sgi.LearningResourceId == resourceId);
        if (existingLink != null) return;

        var link = new SharedResourceGroupItem
        {
            SharedResourceGroupId = groupId,
            LearningResourceId = resourceId
        };
        context.SharedResourceGroupItems.Add(link);
        await context.SaveChangesAsync();
    }

    public async Task RemoveResourceAsync(int groupId, int resourceId, string userId)
    {
        await using var context = await _contextFactory.CreateDbContextAsync();
        // Verify group belongs to user
        var group = await context.SharedResourceGroups
            .FirstOrDefaultAsync(sg => sg.Id == groupId && sg.UserId == userId);
        if (group == null) return;

        var link = await context.SharedResourceGroupItems
            .FirstOrDefaultAsync(sgi => sgi.SharedResourceGroupId == groupId && sgi.LearningResourceId == resourceId);

        if (link != null)
        {
            context.SharedResourceGroupItems.Remove(link);
            await context.SaveChangesAsync();
        }
    }
}
