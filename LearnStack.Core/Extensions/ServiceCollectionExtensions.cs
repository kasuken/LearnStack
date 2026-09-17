using LearnStack.Billing;
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

    /// <summary>
    /// Registers the plan/entitlement system and <see cref="IBillingProvider"/> based on the
    /// <c>Billing</c> configuration section. When <see cref="BillingProviderType.None"/> is
    /// configured (the default), <see cref="NullBillingProvider"/> is registered so the app
    /// starts and works fully without a live payment-provider account.
    /// <see cref="BillingProviderType.Stripe"/> registers <see cref="StripeBillingProvider"/>,
    /// failing fast at startup when any setting it needs is missing - a half-configured payment
    /// provider would otherwise surface as a failed checkout for a real user mid-purchase.
    /// </summary>
    public static IServiceCollection AddLearnStackBilling(
        this IServiceCollection services,
        IConfiguration configuration)
    {
        services.Configure<BillingOptions>(configuration.GetSection(BillingOptions.SectionName));

        var options = configuration
            .GetSection(BillingOptions.SectionName)
            .Get<BillingOptions>() ?? new BillingOptions();

        services.AddScoped<IEntitlementService, EntitlementService>();
        services.AddScoped<IBillingWebhookProcessor, BillingWebhookProcessor>();

        switch (options.Provider)
        {
            case BillingProviderType.None:
                services.AddSingleton<IBillingProvider, NullBillingProvider>();
                return services;

            case BillingProviderType.Stripe:
                ValidateStripeOptions(options);
                // Scoped, not singleton: the provider reads the per-user billing link through
                // the scoped IDbContextFactory<ApplicationDbContext>.
                services.AddScoped<IBillingProvider, StripeBillingProvider>();
                return services;

            default:
                throw new NotSupportedException(
                    $"Billing provider '{options.Provider}' is not implemented yet. Implement IBillingProvider " +
                    "and register it in ServiceCollectionExtensions.AddLearnStackBilling.");
        }
    }

    /// <summary>
    /// Fails startup with one message naming every missing Stripe setting, rather than one
    /// round-trip per fix.
    /// </summary>
    private static void ValidateStripeOptions(BillingOptions options)
    {
        var missing = new List<string>();

        void Require(string? value, string key)
        {
            if (string.IsNullOrWhiteSpace(value))
                missing.Add($"{BillingOptions.SectionName}:{key}");
        }

        Require(options.ApiKey, nameof(BillingOptions.ApiKey));
        Require(options.WebhookSigningSecret, nameof(BillingOptions.WebhookSigningSecret));
        Require(options.ProMonthlyPriceId, nameof(BillingOptions.ProMonthlyPriceId));
        Require(options.ProYearlyPriceId, nameof(BillingOptions.ProYearlyPriceId));
        Require(options.CheckoutSuccessUrl, nameof(BillingOptions.CheckoutSuccessUrl));
        Require(options.CheckoutCancelUrl, nameof(BillingOptions.CheckoutCancelUrl));
        Require(options.PortalReturnUrl, nameof(BillingOptions.PortalReturnUrl));

        if (missing.Count > 0)
        {
            throw new InvalidOperationException(
                "Billing:Provider is 'Stripe' but these settings are missing: " +
                $"{string.Join(", ", missing)}. Supply them (user-secrets or environment " +
                "variables for the secrets) or set Billing:Provider to 'None'.");
        }
    }
}
