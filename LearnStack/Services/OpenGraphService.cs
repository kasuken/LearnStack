using HtmlAgilityPack;

namespace LearnStack.Services;

public class OpenGraphService : IOpenGraphService
{
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
            if (string.IsNullOrWhiteSpace(url) || !Uri.TryCreate(url, UriKind.Absolute, out _))
            {
                _logger.LogWarning("Invalid URL provided: {Url}", url);
                return null;
            }

            var response = await _httpClient.GetAsync(url);
            
            if (!response.IsSuccessStatusCode)
            {
                _logger.LogWarning("Failed to fetch URL: {Url}, Status: {Status}", url, response.StatusCode);
                return null;
            }

            var html = await response.Content.ReadAsStringAsync();
            var htmlDoc = new HtmlDocument();
            htmlDoc.LoadHtml(html);

            var ogData = new OpenGraphData();

            // Extract OpenGraph meta tags
            ogData.Title = GetMetaContent(htmlDoc, "og:title") 
                          ?? GetMetaContent(htmlDoc, "twitter:title")
                          ?? htmlDoc.DocumentNode.SelectSingleNode("//title")?.InnerText?.Trim();

            ogData.Description = GetMetaContent(htmlDoc, "og:description") 
                                ?? GetMetaContent(htmlDoc, "twitter:description")
                                ?? GetMetaContent(htmlDoc, "description");

            ogData.ImageUrl = GetMetaContent(htmlDoc, "og:image") 
                             ?? GetMetaContent(htmlDoc, "twitter:image")
                             ?? GetMetaContent(htmlDoc, "twitter:image:src");

            ogData.SiteName = GetMetaContent(htmlDoc, "og:site_name");
            ogData.Type = GetMetaContent(htmlDoc, "og:type");

            // Try to download the image if an image URL is found
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

    private string? GetMetaContent(HtmlDocument doc, string property)
    {
        // Try og: property attribute
        var node = doc.DocumentNode.SelectSingleNode($"//meta[@property='{property}']");
        if (node != null)
        {
            return node.GetAttributeValue("content", null)?.Trim();
        }

        // Try name attribute
        node = doc.DocumentNode.SelectSingleNode($"//meta[@name='{property}']");
        if (node != null)
        {
            return node.GetAttributeValue("content", null)?.Trim();
        }

        return null;
    }

    private async Task<byte[]?> DownloadImageAsync(string imageUrl)
    {
        try
        {
            // Make sure the image URL is absolute
            if (!Uri.TryCreate(imageUrl, UriKind.Absolute, out var uri))
            {
                _logger.LogWarning("Invalid image URL: {ImageUrl}", imageUrl);
                return null;
            }

            var response = await _httpClient.GetAsync(uri);
            
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
            
            // Limit image size to 5MB
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

