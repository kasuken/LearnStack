namespace LearnStack;

/// <summary>
/// The site's public origin, read once from the <c>Site:BaseUrl</c> setting. Every canonical,
/// share-card, structured-data, robots and sitemap URL is built from it so the domain lives
/// in exactly one place.
/// </summary>
public sealed class SiteUrls
{
    public const string DefaultBaseUrl = "https://learnstack.cloud";

    public SiteUrls(IConfiguration configuration)
    {
        var configured = configuration["Site:BaseUrl"];
        var valid = Uri.TryCreate(configured, UriKind.Absolute, out var uri)
                    && (uri.Scheme == Uri.UriSchemeHttps || uri.Scheme == Uri.UriSchemeHttp);
        BaseUrl = (valid ? configured! : DefaultBaseUrl).TrimEnd('/');
    }

    /// <summary>The origin without a trailing slash, e.g. <c>https://learnstack.cloud</c>.</summary>
    public string BaseUrl { get; }

    /// <summary>Builds an absolute URL for a site-relative path ("/" yields the origin with a trailing slash).</summary>
    public string Url(string path = "/") => $"{BaseUrl}/{path.TrimStart('/')}";
}
