using LearnStack.Data;
using LearnStack.Services;
using Microsoft.AspNetCore.Identity;
using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.Configuration;
using Microsoft.Extensions.DependencyInjection;
using System.Net.Http;

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
        services.AddScoped<IAccountDeletionService, AccountDeletionService>();
        services.AddHttpClient<IOpenGraphService, OpenGraphService>(client =>
        {
            client.Timeout = TimeSpan.FromSeconds(30);
        })
        .ConfigurePrimaryHttpMessageHandler(() => new SocketsHttpHandler
        {
            // Redirects are followed manually by OpenGraphService so every hop can be
            // re-validated against the private-IP/loopback/scheme/port guard.
            AllowAutoRedirect = false,
            ConnectCallback = SafeSocketConnectCallback.ConnectAsync
        });
        return services;
    }

    public static IServiceCollection AddLearnStackEmailSender(
        this IServiceCollection services,
        IConfiguration configuration)
    {
        services.Configure<EmailSenderOptions>(configuration.GetSection(EmailSenderOptions.SectionName));
        services.AddSingleton<ISmtpClient, SmtpClientWrapper>();
        services.AddSingleton<IEmailSender<ApplicationUser>, SmtpEmailSender>();
        return services;
    }
}
