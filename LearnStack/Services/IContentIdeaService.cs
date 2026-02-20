﻿using LearnStack.Data.Models;

namespace LearnStack.Services;

public interface IContentIdeaService
{
    Task<List<ContentIdea>> GetAllAsync(string userId);
    Task<ContentIdea?> GetByIdAsync(int id, string userId);
    Task<ContentIdea> CreateAsync(ContentIdea idea);
    Task<ContentIdea?> UpdateAsync(ContentIdea idea, string userId);
    Task DeleteAsync(int id, string userId);
    Task AddSourceResourceAsync(int ideaId, int resourceId, string userId);
    Task RemoveSourceResourceAsync(int ideaId, int resourceId, string userId);
    Task UpdateOrderAsync(string userId, List<int> orderedIds);
}

