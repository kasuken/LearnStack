using System.Text;

namespace LearnStack.Extensions;

public static class SeoEndpointExtensions
{
    /// <summary>
    /// Serves robots.txt and sitemap.xml generated from <see cref="SiteUrls"/>, so the public
    /// domain is never hard-coded in static files.
    /// </summary>
    public static IEndpointRouteBuilder MapSeoEndpoints(this IEndpointRouteBuilder endpoints)
    {
        endpoints.MapGet("/robots.txt", (SiteUrls site) => Results.Text(RobotsTxt(site), "text/plain", Encoding.UTF8));
        endpoints.MapGet("/sitemap.xml", (SiteUrls site) => Results.Text(SitemapXml(site), "application/xml", Encoding.UTF8));
        return endpoints;
    }

    internal static string RobotsTxt(SiteUrls site) =>
        "# robots.txt for LearnStack\n" +
        "User-agent: *\n" +
        "Allow: /\n\n" +
        "# Account management pages are not for crawlers\n" +
        "Disallow: /Account/\n" +
        "Disallow: /Identity/\n\n" +
        $"Sitemap: {site.Url("sitemap.xml")}\n";

    internal static string SitemapXml(SiteUrls site) =>
        "<?xml version=\"1.0\" encoding=\"UTF-8\"?>\n" +
        "<urlset xmlns=\"http://www.sitemaps.org/schemas/sitemap/0.9\">\n" +
        $"  <url><loc>{System.Security.SecurityElement.Escape(site.Url("/"))}</loc><changefreq>weekly</changefreq><priority>1.0</priority></url>\n" +
        $"  <url><loc>{System.Security.SecurityElement.Escape(site.Url("privacy"))}</loc><changefreq>monthly</changefreq><priority>0.3</priority></url>\n" +
        "</urlset>\n";
}
