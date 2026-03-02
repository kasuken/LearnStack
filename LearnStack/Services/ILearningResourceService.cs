using LearnStack.Data.Models;
using Microsoft.EntityFrameworkCore;

namespace LearnStack.Services;

public interface ILearningResourceService
{
    Task<List<LearningResource>> GetAllAsync(string userId);
    Task<List<LearningResource>> GetByStatusAsync(string userId, ContentStatus status);
    Task<LearningResource?> GetByIdAsync(int id, string userId);
    Task<bool> UrlExistsAsync(string userId, string url, int? excludeResourceId = null);
    Task<LearningResource> CreateAsync(LearningResource resource);
    Task<LearningResource?> UpdateAsync(LearningResource resource, string userId);
    Task DeleteAsync(int id, string userId);
    Task<List<LearningResource>> SearchAsync(string userId, string searchTerm);
    Task UpdateOrderAsync(string userId, List<int> orderedIds);
    Task<bool> ToggleArchiveAsync(int id, string userId);
    Task ArchiveStaleResourcesAsync(string userId);
}

