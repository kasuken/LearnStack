using Microsoft.AspNetCore.Localization;
using System.Globalization;

namespace LearnStack.Extensions;

public static class LocalizationExtensions
{
    private static readonly string[] SupportedCultureNames = ["en", "de", "es", "fr", "it", "ro"];

    public static IServiceCollection AddLearnStackLocalization(this IServiceCollection services)
    {
        services.AddLocalization();

        var supportedCultures = SupportedCultureNames
            .Select(c => new CultureInfo(c))
            .ToList();

        services.Configure<RequestLocalizationOptions>(options =>
        {
            options.DefaultRequestCulture = new RequestCulture("en");
            options.SupportedCultures = supportedCultures;
            options.SupportedUICultures = supportedCultures;

            options.RequestCultureProviders =
            [
                new CookieRequestCultureProvider(),
                new AcceptLanguageHeaderRequestCultureProvider()
            ];
        });

        return services;
    }
}
