using LearnStack.Data;
using LearnStack.Data.Models;
using Microsoft.EntityFrameworkCore;

namespace LearnStack.Services;

public class SharedResourceGroupService : ISharedResourceGroupService
{
    private readonly ApplicationDbContext _context;

    public SharedResourceGroupService(ApplicationDbContext context)
    {
        _context = context;
    }

    public async Task<List<SharedResourceGroup>> GetAllAsync(string userId)
    {
        return await _context.SharedResourceGroups
            .Include(sg => sg.Items)
                .ThenInclude(sgi => sgi.LearningResource)
            .Where(sg => sg.UserId == userId)
            .OrderByDescending(sg => sg.DateCreated)
            .ToListAsync();
    }

    public async Task<SharedResourceGroup?> GetByIdAsync(int id, string userId)
    {
        return await _context.SharedResourceGroups
            .Include(sg => sg.Items)
                .ThenInclude(sgi => sgi.LearningResource)
            .FirstOrDefaultAsync(sg => sg.Id == id && sg.UserId == userId);
    }

    public async Task<SharedResourceGroup?> GetByShareTokenAsync(string shareToken)
    {
        return await _context.SharedResourceGroups
            .Include(sg => sg.Items)
                .ThenInclude(sgi => sgi.LearningResource)
            .Include(sg => sg.User)
            .FirstOrDefaultAsync(sg => sg.ShareToken == shareToken && sg.IsActive);
    }

    public async Task<SharedResourceGroup> CreateAsync(SharedResourceGroup group)
    {
        _context.SharedResourceGroups.Add(group);
        await _context.SaveChangesAsync();
        return group;
    }

    public async Task<SharedResourceGroup?> UpdateAsync(SharedResourceGroup group, string userId)
    {
        var existing = await _context.SharedResourceGroups
            .FirstOrDefaultAsync(sg => sg.Id == group.Id && sg.UserId == userId);
        if (existing == null) return null;

        existing.Name = group.Name;
        existing.Description = group.Description;
        existing.IsActive = group.IsActive;

        await _context.SaveChangesAsync();
        return existing;
    }

    public async Task DeleteAsync(int id, string userId)
    {
        var group = await _context.SharedResourceGroups
            .FirstOrDefaultAsync(sg => sg.Id == id && sg.UserId == userId);
        if (group != null)
        {
            _context.SharedResourceGroups.Remove(group);
            await _context.SaveChangesAsync();
        }
    }

    public async Task AddResourceAsync(int groupId, int resourceId, string userId)
    {
        // Verify group belongs to user
        var group = await _context.SharedResourceGroups
            .FirstOrDefaultAsync(sg => sg.Id == groupId && sg.UserId == userId);
        if (group == null) return;

        // Verify resource belongs to user
        var resource = await _context.LearningResources
            .FirstOrDefaultAsync(lr => lr.Id == resourceId && lr.UserId == userId);
        if (resource == null) return;

        // Check if link already exists
        var existingLink = await _context.SharedResourceGroupItems
            .FirstOrDefaultAsync(sgi => sgi.SharedResourceGroupId == groupId && sgi.LearningResourceId == resourceId);
        if (existingLink != null) return;

        var link = new SharedResourceGroupItem
        {
            SharedResourceGroupId = groupId,
            LearningResourceId = resourceId
        };
        _context.SharedResourceGroupItems.Add(link);
        await _context.SaveChangesAsync();
    }

    public async Task RemoveResourceAsync(int groupId, int resourceId, string userId)
    {
        // Verify group belongs to user
        var group = await _context.SharedResourceGroups
            .FirstOrDefaultAsync(sg => sg.Id == groupId && sg.UserId == userId);
        if (group == null) return;

        var link = await _context.SharedResourceGroupItems
            .FirstOrDefaultAsync(sgi => sgi.SharedResourceGroupId == groupId && sgi.LearningResourceId == resourceId);

        if (link != null)
        {
            _context.SharedResourceGroupItems.Remove(link);
            await _context.SaveChangesAsync();
        }
    }
}
