using LearnStack.Data;
using LearnStack.Services;
using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.DependencyInjection;

namespace LearnStack.Extensions;

public static class ServiceCollectionExtensions
{
    public static IServiceCollection AddLearnStackData(
        this IServiceCollection services,
        string connectionString,
        string? migrationsAssembly = null)
    {
        services.AddDbContextFactory<ApplicationDbContext>(options =>
            options.UseSqlServer(connectionString, sql =>
            {
                if (migrationsAssembly is not null)
                    sql.MigrationsAssembly(migrationsAssembly);
            }));
        return services;
    }

    public static IServiceCollection AddLearnStackApplicationServices(this IServiceCollection services)
    {
        services.AddScoped<ILearningResourceService, LearningResourceService>();
        services.AddScoped<IContentIdeaService, ContentIdeaService>();
        services.AddScoped<ISharedResourceGroupService, SharedResourceGroupService>();
        services.AddScoped<IFriendshipService, FriendshipService>();
        services.AddHttpClient<IOpenGraphService, OpenGraphService>(client =>
        {
            client.Timeout = TimeSpan.FromSeconds(30);
        });
        return services;
    }
}
