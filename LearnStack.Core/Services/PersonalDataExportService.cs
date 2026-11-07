using LearnStack.Data;
using Microsoft.EntityFrameworkCore;

namespace LearnStack.Services;

/// <summary>Builds the GDPR Article 15/20 export of everything a user owns, as plain data.</summary>
public interface IPersonalDataExportService
{
    /// <summary>Returns every user-owned record, or null when the user does not exist.</summary>
    Task<IReadOnlyDictionary<string, object?>?> ExportAsync(string userId, CancellationToken cancellationToken = default);
}

public class PersonalDataExportService(IDbContextFactory<ApplicationDbContext> contextFactory) : IPersonalDataExportService
{
    public async Task<IReadOnlyDictionary<string, object?>?> ExportAsync(string userId, CancellationToken cancellationToken = default)
    {
        ArgumentException.ThrowIfNullOrEmpty(userId);

        await using var context = await contextFactory.CreateDbContextAsync(cancellationToken);

        var exists = await context.Users.AnyAsync(u => u.Id == userId, cancellationToken);
        if (!exists) return null;

        var resources = await context.LearningResources.AsNoTracking()
            .Where(r => r.UserId == userId)
            .OrderBy(r => r.Id)
            .Select(r => new
            {
                r.Id, r.Url, r.Title, r.Description,
                ContentType = r.ContentType.ToString(),
                Status = r.Status.ToString(),
                Priority = r.Priority.ToString(),
                r.Notes, r.DateAdded, r.DateCompleted, r.Tags, r.ThumbnailUrl,
                r.CustomOrder, r.IsArchived, r.IsPublic,
            })
            .ToListAsync(cancellationToken);

        var ideas = await context.ContentIdeas.AsNoTracking()
            .Where(i => i.UserId == userId)
            .OrderBy(i => i.Id)
            .Select(i => new
            {
                i.Id, i.Title,
                ContentType = i.ContentType.ToString(),
                i.Description, i.Outline,
                Status = i.Status.ToString(),
                Priority = i.Priority.ToString(),
                i.Notes, i.DateCreated, i.DatePublished, i.CustomOrder,
                SourceResourceIds = i.SourceResources.Select(s => s.LearningResourceId).ToList(),
            })
            .ToListAsync(cancellationToken);

        var collections = await context.SharedResourceGroups.AsNoTracking()
            .Where(g => g.UserId == userId)
            .OrderBy(g => g.Id)
            .Select(g => new
            {
                g.Id, g.Name, g.Description, g.ShareToken, g.IsActive, g.DateCreated,
                ResourceIds = g.Items.Select(i => i.LearningResourceId).ToList(),
            })
            .ToListAsync(cancellationToken);

        var friendships = await context.LearnerFriendships.AsNoTracking()
            .Where(f => f.RequesterId == userId || f.AddresseeId == userId)
            .OrderBy(f => f.Id)
            .Select(f => new
            {
                f.Id, f.DateCreated,
                Direction = f.RequesterId == userId ? "sent" : "received",
                OtherUserId = f.RequesterId == userId ? f.AddresseeId : f.RequesterId,
            })
            .ToListAsync(cancellationToken);

        var invitations = await context.FriendInvitations.AsNoTracking()
            .Where(i => i.InviterId == userId || i.AcceptedByUserId == userId)
            .OrderBy(i => i.Id)
            .Select(i => new
            {
                i.Id, i.CreatedAt, i.ExpiresAt, i.IsUsed, i.AcceptedAt,
                Role = i.InviterId == userId ? "inviter" : "accepted",
            })
            .ToListAsync(cancellationToken);

        var plan = await context.UserPlans.AsNoTracking()
            .Where(p => p.UserId == userId)
            .Select(p => new
            {
                Tier = p.Tier.ToString(),
                p.PlanRenewsAtUtc, p.PlanCancelsAtUtc, p.GracePeriodEndsAtUtc,
            })
            .FirstOrDefaultAsync(cancellationToken);

        return new Dictionary<string, object?>
        {
            ["LearningResources"] = resources,
            ["ContentIdeas"] = ideas,
            ["SharedCollections"] = collections,
            ["Friendships"] = friendships,
            ["FriendInvitations"] = invitations,
            ["Plan"] = plan,
        };
    }
}
