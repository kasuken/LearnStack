using Microsoft.AspNetCore.Localization;
using Microsoft.AspNetCore.Mvc;

namespace LearnStack.Controllers;

[Route("[controller]/[action]")]
public class CultureController : Controller
{
    private static readonly HashSet<string> SupportedCultures = new(StringComparer.OrdinalIgnoreCase)
    {
        "en", "de", "es", "fr", "it"
    };

    [IgnoreAntiforgeryToken]
    public IActionResult Set(string culture, string returnUrl)
    {
        var normalizedCulture = string.IsNullOrWhiteSpace(culture) ? "en" : culture.Trim().ToLowerInvariant();
        if (!SupportedCultures.Contains(normalizedCulture))
        {
            normalizedCulture = "en";
        }

        Response.Cookies.Append(
            CookieRequestCultureProvider.DefaultCookieName,
            CookieRequestCultureProvider.MakeCookieValue(new RequestCulture(normalizedCulture)),
            new CookieOptions { Expires = DateTimeOffset.UtcNow.AddYears(1), IsEssential = true }
        );

        return LocalRedirect(returnUrl ?? "/");
    }
}
