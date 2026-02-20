namespace LearnStack.Services;

public interface IOpenGraphService
{
    Task<OpenGraphData?> FetchMetadataAsync(string url);
}

public class OpenGraphData
{
    public string? Title { get; set; }
    public string? Description { get; set; }
    public string? ImageUrl { get; set; }
    public string? SiteName { get; set; }
    public string? Type { get; set; }
    public byte[]? ImageData { get; set; }
}

