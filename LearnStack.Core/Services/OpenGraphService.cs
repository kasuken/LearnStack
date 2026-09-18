using HtmlAgilityPack;
using Microsoft.Extensions.Logging;
using System.Net;
using System.Net.Sockets;
using System.Text;
using System.Text.Json;

namespace LearnStack.Services;

public class OpenGraphService : IOpenGraphService
{
    // Maximum number of redirect hops we will follow. Every hop is re-validated against the
    // same scheme/port/private-IP guard as the original URL, so a public URL cannot bounce
    // through a redirect chain into an internal or cloud-metadata address (e.g. 169.254.169.254).
    private const int MaxRedirects = 5;

    // Cap the HTML body we buffer in memory. The response is read incrementally and abandoned
    // as soon as this is exceeded, so a large or endless response can't exhaust memory.
    private const int MaxHtmlBytes = 1 * 1024 * 1024; // 1 MB

    private static readonly HttpStatusCode[] RedirectStatusCodes =
    [
        HttpStatusCode.MovedPermanently,
        HttpStatusCode.Found,
        HttpStatusCode.SeeOther,
        HttpStatusCode.TemporaryRedirect,
        HttpStatusCode.PermanentRedirect
    ];

    private readonly HttpClient _httpClient;
    private readonly ILogger<OpenGraphService> _logger;

    public OpenGraphService(HttpClient httpClient, ILogger<OpenGraphService> logger)
    {
        _httpClient = httpClient;
        _logger = logger;

        // Set a user agent to avoid being blocked by some websites
        _httpClient.DefaultRequestHeaders.Add("User-Agent",
            "Mozilla/5.0 (Windows NT 10.0; Win64; x64) AppleWebKit/537.36 (KHTML, like Gecko) Chrome/120.0.0.0 Safari/537.36");
    }

    public async Task<OpenGraphData?> FetchMetadataAsync(string url)
    {
        try
        {
            if (string.IsNullOrWhiteSpace(url) || !Uri.TryCreate(url, UriKind.Absolute, out var uri))
            {
                _logger.LogWarning("Invalid URL provided: {Url}", url);
                return null;
            }

            if (await IsBlockedUriAsync(uri))
            {
                _logger.LogWarning("Blocked metadata URL: {Url}", url);
                return null;
            }

            // YouTube pages are frequently protected and can return consent pages.
            // Use oEmbed first to reliably fetch video metadata without API keys.
            if (IsYouTubeUrl(uri))
            {
                var youtubeData = await FetchYouTubeMetadataAsync(uri);
                if (youtubeData != null)
                {
                    return youtubeData;
                }
            }

            using var response = await GetWithRedirectValidationAsync(uri);

            if (response == null)
            {
                _logger.LogWarning("Blocked or unresolvable redirect chain for URL: {Url}", url);
                return null;
            }

            if (!response.IsSuccessStatusCode)
            {
                _logger.LogWarning("Failed to fetch URL: {Url}, Status: {Status}", url, response.StatusCode);
                return null;
            }

            var html = await ReadCappedStringAsync(response, MaxHtmlBytes);
            if (html == null)
            {
                _logger.LogWarning("Response body exceeded the {MaxBytes} byte limit: {Url}", MaxHtmlBytes, url);
                return null;
            }

            var htmlDoc = new HtmlDocument();
            htmlDoc.LoadHtml(html);

            var ogData = new OpenGraphData
            {
                Title = GetMetaContent(htmlDoc, "og:title")
                        ?? GetMetaContent(htmlDoc, "twitter:title")
                        ?? htmlDoc.DocumentNode.SelectSingleNode("//title")?.InnerText?.Trim(),
                Description = GetMetaContent(htmlDoc, "og:description")
                              ?? GetMetaContent(htmlDoc, "twitter:description")
                              ?? GetMetaContent(htmlDoc, "description"),
                ImageUrl = GetMetaContent(htmlDoc, "og:image")
                           ?? GetMetaContent(htmlDoc, "twitter:image")
                           ?? GetMetaContent(htmlDoc, "twitter:image:src"),
                SiteName = GetMetaContent(htmlDoc, "og:site_name"),
                Type = GetMetaContent(htmlDoc, "og:type")
            };

            if (!string.IsNullOrWhiteSpace(ogData.ImageUrl))
            {
                ogData.ImageData = await DownloadImageAsync(ogData.ImageUrl);
            }

            return ogData;
        }
        catch (Exception ex)
        {
            _logger.LogError(ex, "Error fetching OpenGraph data from URL: {Url}", url);
            return null;
        }
    }

    /// <summary>
    /// Issues a GET request and manually follows redirects (HttpClient auto-redirect is disabled
    /// at the handler level), re-validating the scheme/port/private-IP guard on every hop. This
    /// closes the gap where a public URL 302s to a private/loopback/cloud-metadata address.
    /// </summary>
    private async Task<HttpResponseMessage?> GetWithRedirectValidationAsync(Uri uri)
    {
        var current = uri;

        for (var attempt = 0; attempt <= MaxRedirects; attempt++)
        {
            if (await IsBlockedUriAsync(current))
            {
                _logger.LogWarning("Blocked URL encountered while following redirects: {Url}", current);
                return null;
            }

            var response = await _httpClient.GetAsync(current, HttpCompletionOption.ResponseHeadersRead);

            if (!RedirectStatusCodes.Contains(response.StatusCode))
            {
                return response;
            }

            var location = response.Headers.Location;
            response.Dispose();

            if (location == null)
            {
                _logger.LogWarning("Redirect response missing Location header: {Url}", current);
                return null;
            }

            current = location.IsAbsoluteUri ? location : new Uri(current, location);
        }

        _logger.LogWarning("Too many redirects ({MaxRedirects}) while fetching URL: {Url}", MaxRedirects, uri);
        return null;
    }

    /// <summary>
    /// Reads the response body up to <paramref name="maxBytes"/>. Reading stops and the partial
    /// buffer is discarded as soon as the cap is exceeded, so the full body is never buffered.
    /// </summary>
    private static async Task<string?> ReadCappedStringAsync(HttpResponseMessage response, int maxBytes)
    {
        if (response.Content.Headers.ContentLength is long contentLength && contentLength > maxBytes)
        {
            return null;
        }

        await using var stream = await response.Content.ReadAsStreamAsync();
        using var buffer = new MemoryStream();
        var chunk = new byte[8192];
        var totalRead = 0;

        int read;
        while ((read = await stream.ReadAsync(chunk)) > 0)
        {
            totalRead += read;
            if (totalRead > maxBytes)
            {
                return null;
            }

            buffer.Write(chunk, 0, read);
        }

        buffer.Position = 0;
        using var reader = new StreamReader(buffer, DetectEncoding(response));
        return await reader.ReadToEndAsync();
    }

    private static Encoding DetectEncoding(HttpResponseMessage response)
    {
        var charset = response.Content.Headers.ContentType?.CharSet;
        if (!string.IsNullOrWhiteSpace(charset))
        {
            try
            {
                return Encoding.GetEncoding(charset);
            }
            catch (ArgumentException)
            {
                // Unknown/invalid charset - fall back to UTF-8 below.
            }
        }

        return Encoding.UTF8;
    }

    private async Task<OpenGraphData?> FetchYouTubeMetadataAsync(Uri url)
    {
        try
        {
            var oEmbedUrl =
                $"https://www.youtube.com/oembed?url={Uri.EscapeDataString(url.ToString())}&format=json";

            var response = await _httpClient.GetAsync(oEmbedUrl);
            if (!response.IsSuccessStatusCode)
            {
                _logger.LogWarning("YouTube oEmbed failed for URL: {Url}, Status: {Status}",
                    url, response.StatusCode);
                return null;
            }

            await using var stream = await response.Content.ReadAsStreamAsync();
            using var json = await JsonDocument.ParseAsync(stream);
            var root = json.RootElement;

            string? title = root.TryGetProperty("title", out var titleElement)
                ? titleElement.GetString()
                : null;

            string? thumbnailUrl = root.TryGetProperty("thumbnail_url", out var thumbnailElement)
                ? thumbnailElement.GetString()
                : null;

            var ogData = new OpenGraphData
            {
                Title = title,
                Description = null,
                ImageUrl = thumbnailUrl,
                SiteName = "YouTube",
                Type = "video"
            };

            if (!string.IsNullOrWhiteSpace(ogData.ImageUrl))
            {
                ogData.ImageData = await DownloadImageAsync(ogData.ImageUrl);
            }

            return ogData;
        }
        catch (Exception ex)
        {
            _logger.LogWarning(ex, "Error fetching YouTube metadata for URL: {Url}", url);
            return null;
        }
    }

    private static bool IsYouTubeUrl(Uri uri)
    {
        var host = uri.Host.ToLowerInvariant();

        return host == "youtube.com"
               || host.EndsWith(".youtube.com", StringComparison.Ordinal)
               || host == "youtu.be";
    }

    private async Task<bool> IsBlockedUriAsync(Uri uri)
    {
        if (!IsAllowedScheme(uri) || !IsAllowedPort(uri))
        {
            return true;
        }

        return await IsPrivateOrLoopbackAddressAsync(uri);
    }

    private async Task<bool> IsPrivateOrLoopbackAddressAsync(Uri uri)
    {
        if (uri.IsLoopback)
        {
            return true;
        }

        try
        {
            var addresses = await Dns.GetHostAddressesAsync(uri.DnsSafeHost);
            return addresses.Any(IsPrivateOrLoopbackIpAddress);
        }
        catch (SocketException ex)
        {
            _logger.LogWarning(ex, "Failed to resolve host for URL: {Host}", uri.DnsSafeHost);
            return true;
        }
    }

    private static bool IsAllowedScheme(Uri uri)
        => uri.Scheme == Uri.UriSchemeHttp || uri.Scheme == Uri.UriSchemeHttps;

    // Restrict requests to the standard web ports. Uri.Port resolves to the scheme's default
    // (80/443) when none is specified, so this only rejects explicit non-standard ports.
    private static bool IsAllowedPort(Uri uri)
        => uri.Port is 80 or 443;

    // Internal so it can be reused by SafeSocketConnectCallback, which validates the IP that a
    // SocketsHttpHandler is about to connect to, closing the TOCTOU/DNS-rebinding gap between
    // the validation resolve above and the resolve the request itself would otherwise perform.
    internal static bool IsPrivateOrLoopbackIpAddress(IPAddress address)
    {
        if (IPAddress.IsLoopback(address))
        {
            return true;
        }

        if (address.IsIPv4MappedToIPv6)
        {
            return IsPrivateOrLoopbackIpAddress(address.MapToIPv4());
        }

        if (address.AddressFamily == AddressFamily.InterNetwork)
        {
            var bytes = address.GetAddressBytes();
            return bytes[0] == 10
                || (bytes[0] == 172 && bytes[1] >= 16 && bytes[1] <= 31)
                || (bytes[0] == 192 && bytes[1] == 168)
                || (bytes[0] == 169 && bytes[1] == 254);
        }

        if (address.AddressFamily == AddressFamily.InterNetworkV6)
        {
            return address.IsIPv6LinkLocal
                || address.IsIPv6SiteLocal
                || address.IsIPv6Multicast
                || address.IsIPv6UniqueLocal;
        }

        return false;
    }

    private string? GetMetaContent(HtmlDocument doc, string property)
    {
        var node = doc.DocumentNode.SelectSingleNode($"//meta[@property='{property}']");
        if (node != null)
        {
            return node.GetAttributeValue("content", "")?.Trim();
        }

        node = doc.DocumentNode.SelectSingleNode($"//meta[@name='{property}']");
        if (node != null)
        {
            return node.GetAttributeValue("content", "")?.Trim();
        }

        return null;
    }

    private async Task<byte[]?> DownloadImageAsync(string imageUrl)
    {
        try
        {
            if (!Uri.TryCreate(imageUrl, UriKind.Absolute, out var uri))
            {
                _logger.LogWarning("Invalid image URL: {ImageUrl}", imageUrl);
                return null;
            }

            if (await IsBlockedUriAsync(uri))
            {
                _logger.LogWarning("Blocked image URL: {ImageUrl}", imageUrl);
                return null;
            }

            using var response = await GetWithRedirectValidationAsync(uri);

            if (response == null)
            {
                _logger.LogWarning("Blocked or unresolvable redirect chain for image URL: {ImageUrl}", imageUrl);
                return null;
            }

            if (!response.IsSuccessStatusCode)
            {
                _logger.LogWarning("Failed to download image: {ImageUrl}, Status: {Status}",
                    imageUrl, response.StatusCode);
                return null;
            }

            var contentType = response.Content.Headers.ContentType?.MediaType;
            if (contentType == null || !contentType.StartsWith("image/", StringComparison.OrdinalIgnoreCase))
            {
                _logger.LogWarning("Downloaded content is not an image: {ImageUrl}, Content-Type: {ContentType}",
                    imageUrl, contentType);
                return null;
            }

            var imageData = await response.Content.ReadAsByteArrayAsync();

            if (imageData.Length > 5 * 1024 * 1024)
            {
                _logger.LogWarning("Image too large: {ImageUrl}, Size: {Size}", imageUrl, imageData.Length);
                return null;
            }

            return imageData;
        }
        catch (Exception ex)
        {
            _logger.LogError(ex, "Error downloading image from URL: {ImageUrl}", imageUrl);
            return null;
        }
    }
}
