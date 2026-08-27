using LearnStack.Data.Models;
using LearnStack.Services;

namespace LearnStack.Core.Tests.Services;

public class ContentIdeaServiceTests
{
 private static readonly string UserId = Text('u', 's', 'e', 'r', '-', '1');
 private static readonly string OtherUserId = Text('u', 's', 'e', 'r', '-', '2');

 private static Task<TestDbContextFactory> MakeFactory()
 => TestDbContextFactory.CreateAsync(UserId, OtherUserId);

 [Fact]
 public async Task CreateAsync_StoresAndReturnsIdea()
 {
 await using var factory = await MakeFactory();
 var svc = new ContentIdeaService(factory);

 var idea = await svc.CreateAsync(MakeIdea(UserId, Text('T', 'e', 's', 't', ' ', 'i', 'd', 'e', 'a')));

 Assert.True(idea.Id > 0);

 var fetched = await svc.GetByIdAsync(idea.Id, UserId);
 Assert.NotNull(fetched);
 Assert.Equal(Text('T', 'e', 's', 't', ' ', 'i', 'd', 'e', 'a'), fetched.Title);
 }

 [Fact]
 public async Task GetAllAsync_ReturnsOnlyIdeasForOwner()
 {
 await using var factory = await MakeFactory();
 var svc = new ContentIdeaService(factory);

 await svc.CreateAsync(MakeIdea(UserId, Text('O', 'w', 'n', 'e', 'd', ' ', 'i', 'd', 'e', 'a')));
 await svc.CreateAsync(MakeIdea(OtherUserId, Text('O', 't', 'h', 'e', 'r', ' ', 'i', 'd', 'e', 'a')));

 var results = await svc.GetAllAsync(UserId);

 Assert.Single(results);
 Assert.Equal(Text('O', 'w', 'n', 'e', 'd', ' ', 'i', 'd', 'e', 'a'), results[0].Title);
 }

 [Fact]
 public async Task AddSourceResourceAsync_AddsLinkForOwnedEntities()
 {
 await using var factory = await MakeFactory();
 var svc = new ContentIdeaService(factory);

 var idea = await svc.CreateAsync(MakeIdea(UserId));
 var resource = await CreateResourceAsync(factory, MakeResource(UserId));

 await svc.AddSourceResourceAsync(idea.Id, resource.Id, UserId);

 var fetched = await svc.GetByIdAsync(idea.Id, UserId);
 Assert.NotNull(fetched);
 Assert.Single(fetched.SourceResources);
 Assert.Equal(resource.Id, fetched.SourceResources.Single().LearningResourceId);
 }

 [Fact]
 public async Task AddSourceResourceAsync_DoesNotDuplicateExistingLink()
 {
 await using var factory = await MakeFactory();
 var svc = new ContentIdeaService(factory);

 var idea = await svc.CreateAsync(MakeIdea(UserId));
 var resource = await CreateResourceAsync(factory, MakeResource(UserId));

 await svc.AddSourceResourceAsync(idea.Id, resource.Id, UserId);
 await svc.AddSourceResourceAsync(idea.Id, resource.Id, UserId);

 var fetched = await svc.GetByIdAsync(idea.Id, UserId);
 Assert.NotNull(fetched);
 Assert.Single(fetched.SourceResources);
 }

 [Fact]
 public async Task RemoveSourceResourceAsync_RemovesOwnedLink()
 {
 await using var factory = await MakeFactory();
 var svc = new ContentIdeaService(factory);

 var idea = await svc.CreateAsync(MakeIdea(UserId));
 var resource = await CreateResourceAsync(factory, MakeResource(UserId));
 await svc.AddSourceResourceAsync(idea.Id, resource.Id, UserId);

 await svc.RemoveSourceResourceAsync(idea.Id, resource.Id, UserId);

 var fetched = await svc.GetByIdAsync(idea.Id, UserId);
 Assert.NotNull(fetched);
 Assert.Empty(fetched.SourceResources);
 }

 [Fact]
 public async Task RemoveSourceResourceAsync_DoesNotRemoveLinkWhenResourceBelongsToAnotherUser()
 {
 await using var factory = await MakeFactory();
 var svc = new ContentIdeaService(factory);

 var idea = await svc.CreateAsync(MakeIdea(UserId));
 var otherUsersResource = await CreateResourceAsync(factory, MakeResource(OtherUserId, Text('O', 't', 'h', 'e', 'r', ' ', 'u', 's', 'e', 'r', 's', ' ', 'r', 'e', 's', 'o', 'u', 'r', 'c', 'e')));
 await CreateLinkAsync(factory, idea.Id, otherUsersResource.Id);

 await svc.RemoveSourceResourceAsync(idea.Id, otherUsersResource.Id, UserId);

 var fetched = await svc.GetByIdAsync(idea.Id, UserId);
 Assert.NotNull(fetched);
 Assert.Single(fetched.SourceResources);
 }

 [Fact]
 public async Task UpdateOrderAsync_UpdatesOnlyOwnedIdeas()
 {
 await using var factory = await MakeFactory();
 var svc = new ContentIdeaService(factory);

 var first = await svc.CreateAsync(MakeIdea(UserId, Text('F', 'i', 'r', 's', 't')));
 var second = await svc.CreateAsync(MakeIdea(UserId, Text('S', 'e', 'c', 'o', 'n', 'd')));
 var other = await svc.CreateAsync(MakeIdea(OtherUserId, Text('O', 't', 'h', 'e', 'r')));

 await svc.UpdateOrderAsync(UserId, [second.Id, other.Id, first.Id]);

 var ownedIdeas = await svc.GetAllAsync(UserId);
 Assert.Equal(0, ownedIdeas.Single(ci => ci.Id == second.Id).CustomOrder);
 Assert.Equal(2, ownedIdeas.Single(ci => ci.Id == first.Id).CustomOrder);

 var otherIdea = await svc.GetByIdAsync(other.Id, OtherUserId);
 Assert.NotNull(otherIdea);
 Assert.Equal(0, otherIdea.CustomOrder);
 }

 private static async Task<LearningResource> CreateResourceAsync(TestDbContextFactory factory, LearningResource resource)
 {
 await using var context = await factory.CreateDbContextAsync();
 context.LearningResources.Add(resource);
 await context.SaveChangesAsync();
 return resource;
 }

 private static async Task CreateLinkAsync(TestDbContextFactory factory, int ideaId, int resourceId)
 {
 await using var context = await factory.CreateDbContextAsync();
 context.ContentIdeaResources.Add(new ContentIdeaResource
 {
 ContentIdeaId = ideaId,
 LearningResourceId = resourceId
 });
 await context.SaveChangesAsync();
 }

 private static ContentIdea MakeIdea(string userId, string? title = null) =>
 new()
 {
 UserId = userId,
 Title = title ?? Text('I', 'd', 'e', 'a'),
 ContentType = ContentIdeaType.BlogPost,
 Status = IdeaStatus.Idea,
 Priority = Priority.Medium
 };

 private static LearningResource MakeResource(string userId, string? title = null) =>
 new()
 {
 UserId = userId,
 Title = title ?? Text('R', 'e', 's', 'o', 'u', 'r', 'c', 'e'),
 Url = Text('h', 't', 't', 'p', 's', ':', '/', '/', 'e', 'x', 'a', 'm', 'p', 'l', 'e', '.', 'c', 'o', 'm', '/') + Guid.NewGuid().ToString(Text('N')),
 ContentType = ContentType.BlogPost,
 Status = ContentStatus.ToLearn,
 Priority = Priority.Medium
 };

 private static string Text(params char[] chars) => new(chars);
}
