using System.Globalization;
using Microsoft.AspNetCore.Localization;

namespace LearnStack.Middleware;

public class CultureMiddleware
{
    private readonly RequestDelegate _next;

    private static readonly HashSet<string> SupportedCultures = new(StringComparer.OrdinalIgnoreCase)
    {
        "en", "de", "es", "fr", "it"
    };

    public CultureMiddleware(RequestDelegate next)
    {
        _next = next;
    }           

    public async Task InvokeAsync(HttpContext context)
    {
        // 1. Try to read culture from cookie
        var cultureName = GetCultureFromCookie(context);

        // 2. Fallback: read from Accept-Language header
        if (string.IsNullOrEmpty(cultureName))
        {
            cultureName = GetCultureFromHeader(context);
        }

        // 3. Default to English
        if (string.IsNullOrEmpty(cultureName))
        {
            cultureName = "en";
        }

        var cultureInfo = new CultureInfo(cultureName);

        // Set culture for the current request thread
        CultureInfo.CurrentCulture = cultureInfo;
        CultureInfo.CurrentUICulture = cultureInfo;

        await _next(context);
    }

    private static string? GetCultureFromCookie(HttpContext context)
    {
        var cookieValue = context.Request.Cookies[CookieRequestCultureProvider.DefaultCookieName];
        if (string.IsNullOrEmpty(cookieValue))
            return null;

        var providerResult = CookieRequestCultureProvider.ParseCookieValue(cookieValue);
        var culture = providerResult?.Cultures.FirstOrDefault().Value;

        if (!string.IsNullOrEmpty(culture) && SupportedCultures.Contains(culture))
            return culture;

        return null;
    }

    private static string? GetCultureFromHeader(HttpContext context)
    {
        var acceptLanguage = context.Request.Headers.AcceptLanguage.ToString();
        if (string.IsNullOrWhiteSpace(acceptLanguage))
            return null;

        var preferredLanguage = acceptLanguage.Split(',')[0].Trim();

        // Handle language tags like "en-US" → "en"
        var twoLetterCode = preferredLanguage.Length >= 2
            ? preferredLanguage[..2]
            : preferredLanguage;

        if (SupportedCultures.Contains(twoLetterCode))
            return twoLetterCode;

        return null;
    }
}
