using LearnStack.Services;
using Microsoft.Extensions.Logging.Abstractions;
using Moq;
using Moq.Protected;
using System.Net;
using System.Text;

namespace LearnStack.Core.Tests.Services;

public class OpenGraphServiceTests
{
    private static OpenGraphService BuildService(HttpMessageHandler handler)
    {
        var client = new HttpClient(handler);
        client.Timeout = TimeSpan.FromSeconds(5);
        return new OpenGraphService(client, NullLogger<OpenGraphService>.Instance);
    }

    // -----------------------------------------------------------------------
    // URL validation
    // -----------------------------------------------------------------------

    [Theory]
    [InlineData(null)]
    [InlineData("")]
    [InlineData("   ")]
    [InlineData("not-a-url")]
    public async Task FetchMetadataAsync_WithInvalidUrl_ReturnsNull(string? url)
    {
        var svc = BuildService(new UnreachableHandler());
        var result = await svc.FetchMetadataAsync(url!);
        Assert.Null(result);
    }

    [Theory]
    [InlineData("file:///etc/passwd")]
    [InlineData("ftp://example.com/file")]
    public async Task FetchMetadataAsync_WithDisallowedScheme_ReturnsNull(string url)
    {
        var svc = BuildService(new UnreachableHandler());
        var result = await svc.FetchMetadataAsync(url);
        Assert.Null(result);
    }

    [Theory]
    [InlineData("http://127.0.0.1/")]
    [InlineData("http://localhost/")]
    [InlineData("https://[::1]/")]
    public async Task FetchMetadataAsync_WithLoopbackAddress_ReturnsNull(string url)
    {
        var svc = BuildService(new UnreachableHandler());
        var result = await svc.FetchMetadataAsync(url);
        Assert.Null(result);
    }

    // -----------------------------------------------------------------------
    // HTTP response failures
    // -----------------------------------------------------------------------

    [Fact]
    public async Task FetchMetadataAsync_WhenServerReturns404_ReturnsNull()
    {
        var handler = MockHandler(HttpStatusCode.NotFound, "Not Found", "text/html");
        var svc = BuildService(handler);

        var result = await svc.FetchMetadataAsync("https://example.com/missing");

        Assert.Null(result);
    }

    // -----------------------------------------------------------------------
    // HTML parsing
    // -----------------------------------------------------------------------

    [Fact]
    public async Task FetchMetadataAsync_ParsesOgTitle()
    {
        const string html = """
            <html><head>
              <meta property="og:title" content="Hello World" />
              <meta property="og:description" content="A description" />
            </head><body></body></html>
            """;

        var handler = MockHandler(HttpStatusCode.OK, html, "text/html");
        var svc = BuildService(handler);

        var result = await svc.FetchMetadataAsync("https://example.com/");

        Assert.NotNull(result);
        Assert.Equal("Hello World", result.Title);
        Assert.Equal("A description", result.Description);
    }

    [Fact]
    public async Task FetchMetadataAsync_FallsBackToTitleTagWhenNoOgTitle()
    {
        const string html = "<html><head><title>Page Title</title></head><body></body></html>";
        var handler = MockHandler(HttpStatusCode.OK, html, "text/html");
        var svc = BuildService(handler);

        var result = await svc.FetchMetadataAsync("https://example.com/");

        Assert.NotNull(result);
        Assert.Equal("Page Title", result.Title);
    }

    [Fact]
    public async Task FetchMetadataAsync_WhenImageContentTypeIsNotImage_SkipsImageData()
    {
        const string html = """
            <html><head>
              <meta property="og:title" content="Test" />
              <meta property="og:image" content="https://example.com/not-an-image" />
            </head></html>
            """;

        // First call: page HTML; second call: "image" endpoint returns HTML (not an image)
        var handlerMock = new Mock<HttpMessageHandler>();
        handlerMock.Protected()
            .SetupSequence<Task<HttpResponseMessage>>(
                "SendAsync",
                ItExpr.IsAny<HttpRequestMessage>(),
                ItExpr.IsAny<CancellationToken>())
            .ReturnsAsync(new HttpResponseMessage(HttpStatusCode.OK)
            {
                Content = new StringContent(html, Encoding.UTF8, "text/html")
            })
            .ReturnsAsync(new HttpResponseMessage(HttpStatusCode.OK)
            {
                Content = new StringContent("<html></html>", Encoding.UTF8, "text/html")
            });

        var svc = BuildService(handlerMock.Object);
        var result = await svc.FetchMetadataAsync("https://example.com/page");

        Assert.NotNull(result);
        Assert.Equal("Test", result.Title);
        Assert.Null(result.ImageData); // non-image content → no stored bytes
    }

    // -----------------------------------------------------------------------
    // Helpers
    // -----------------------------------------------------------------------

    private static HttpMessageHandler MockHandler(HttpStatusCode status, string body, string contentType)
    {
        var mock = new Mock<HttpMessageHandler>();
        mock.Protected()
            .Setup<Task<HttpResponseMessage>>(
                "SendAsync",
                ItExpr.IsAny<HttpRequestMessage>(),
                ItExpr.IsAny<CancellationToken>())
            .ReturnsAsync(new HttpResponseMessage(status)
            {
                Content = new StringContent(body, Encoding.UTF8, contentType)
            });
        return mock.Object;
    }

    /// Handler that throws if ever invoked — used for tests where no HTTP call should be made.
    private sealed class UnreachableHandler : HttpMessageHandler
    {
        protected override Task<HttpResponseMessage> SendAsync(
            HttpRequestMessage request, CancellationToken cancellationToken)
            => throw new InvalidOperationException("HTTP call should not have been made.");
    }
}
