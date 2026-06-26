namespace LearnStack.Helpers;

internal static class UrlNormalizer
{
    internal static string Normalize(string url)
    {
        if (string.IsNullOrWhiteSpace(url))
            return string.Empty;

        var trimmedUrl = url.Trim();

        if (Uri.TryCreate(trimmedUrl, UriKind.Absolute, out var uri))
        {
            var builder = new UriBuilder(uri)
            {
                Host = uri.Host.ToLowerInvariant(),
                Scheme = uri.Scheme.ToLowerInvariant()
            };
            return builder.Uri.AbsoluteUri.TrimEnd('/');
        }

        return trimmedUrl.TrimEnd('/').ToLowerInvariant();
    }
}
