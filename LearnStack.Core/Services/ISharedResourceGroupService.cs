using LearnStack.Data.Models;

namespace LearnStack.Services;

public interface ISharedResourceGroupService
{
    Task<List<SharedResourceGroup>> GetAllAsync(string userId);
    Task<SharedResourceGroup?> GetByIdAsync(int id, string userId);
    Task<SharedResourceGroup?> GetByShareTokenAsync(string shareToken);
    Task<SharedResourceGroup> CreateAsync(SharedResourceGroup group);
    Task<SharedResourceGroup?> UpdateAsync(SharedResourceGroup group, string userId);
    Task DeleteAsync(int id, string userId);
    Task AddResourceAsync(int groupId, int resourceId, string userId);
    Task RemoveResourceAsync(int groupId, int resourceId, string userId);
}
