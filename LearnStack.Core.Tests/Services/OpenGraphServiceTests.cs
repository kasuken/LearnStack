using LearnStack.Services;
using Microsoft.Extensions.Logging.Abstractions;
using Moq;
using Moq.Protected;
using System.Net;
using System.Net.Http.Headers;
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
    // Redirect handling (SSRF hardening)
    // -----------------------------------------------------------------------

    [Fact]
    public async Task FetchMetadataAsync_WithRedirectToPrivateAddress_IsRejectedWithoutRequestingIt()
    {
        // A public URL that 302s to a cloud-metadata/private address (e.g. Azure IMDS) must be
        // rejected, and the private target must never actually be requested.
        var handler = new RedirectHandler(new Uri("http://169.254.169.254/latest/meta-data/"));
        var svc = BuildService(handler);

        var result = await svc.FetchMetadataAsync("https://example.com/redirect");

        Assert.Null(result);
        Assert.Equal(1, handler.CallCount);
    }

    [Fact]
    public async Task FetchMetadataAsync_WithRedirectToLoopbackAddress_IsRejectedWithoutRequestingIt()
    {
        var handler = new RedirectHandler(new Uri("http://127.0.0.1/admin"));
        var svc = BuildService(handler);

        var result = await svc.FetchMetadataAsync("https://example.com/redirect");

        Assert.Null(result);
        Assert.Equal(1, handler.CallCount);
    }

    [Fact]
    public async Task FetchMetadataAsync_WithTooManyRedirects_ReturnsNull()
    {
        var handler = new EndlessRedirectHandler();
        var svc = BuildService(handler);

        var result = await svc.FetchMetadataAsync("https://example.com/loop");

        Assert.Null(result);
        Assert.True(handler.CallCount <= 7, "Should stop following redirects after a bounded number of hops.");
    }

    [Fact]
    public async Task FetchMetadataAsync_FollowsRedirectToPublicAddress()
    {
        const string html = """
            <html><head><meta property="og:title" content="Redirected Page" /></head></html>
            """;

        var handler = new RedirectThenContentHandler(new Uri("https://example.org/final"), html);
        var svc = BuildService(handler);

        var result = await svc.FetchMetadataAsync("https://example.com/start");

        Assert.NotNull(result);
        Assert.Equal("Redirected Page", result.Title);
        Assert.Equal(2, handler.CallCount);
    }

    // -----------------------------------------------------------------------
    // Response body size limit
    // -----------------------------------------------------------------------

    [Fact]
    public async Task FetchMetadataAsync_WithOversizedContentLength_IsRejectedWithoutReadingBody()
    {
        var handler = new OversizedContentLengthHandler();
        var svc = BuildService(handler);

        var result = await svc.FetchMetadataAsync("https://example.com/huge");

        Assert.Null(result);
        Assert.False(handler.Content.WasStreamRead, "Body should never be read once Content-Length exceeds the cap.");
    }

    [Fact]
    public async Task FetchMetadataAsync_WithUnboundedBody_AbandonsReadingPastTheCap()
    {
        // Simulates a chunked/streamed response with no Content-Length that never ends. If the
        // service tried to buffer it fully this test would hang; instead it must stop reading
        // shortly after crossing the ~1 MB cap.
        var handler = new UnboundedBodyHandler();
        var svc = BuildService(handler);

        var result = await svc.FetchMetadataAsync("https://example.com/endless");

        Assert.Null(result);
        Assert.True(handler.Content.Stream.TotalBytesProduced is > 1_048_576 and < 2_097_152,
            $"Expected reading to stop shortly after the 1 MB cap, but {handler.Content.Stream.TotalBytesProduced} bytes were produced.");
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

    /// Returns a redirect to <paramref name="location"/> on the first call, then throws if
    /// invoked again — proves a blocked redirect target is never actually requested.
    private sealed class RedirectHandler(Uri location) : HttpMessageHandler
    {
        public int CallCount { get; private set; }

        protected override Task<HttpResponseMessage> SendAsync(
            HttpRequestMessage request, CancellationToken cancellationToken)
        {
            CallCount++;
            if (CallCount > 1)
            {
                throw new InvalidOperationException(
                    "Redirect target should have been blocked before a request was sent.");
            }

            var response = new HttpResponseMessage(HttpStatusCode.Found);
            response.Headers.Location = location;
            return Task.FromResult(response);
        }
    }

    /// Always redirects to a new (safe, public) location — used to verify the redirect chain
    /// is bounded rather than followed indefinitely.
    private sealed class EndlessRedirectHandler : HttpMessageHandler
    {
        public int CallCount { get; private set; }

        protected override Task<HttpResponseMessage> SendAsync(
            HttpRequestMessage request, CancellationToken cancellationToken)
        {
            CallCount++;
            var response = new HttpResponseMessage(HttpStatusCode.Found);
            response.Headers.Location = new Uri($"https://example.com/hop-{CallCount}");
            return Task.FromResult(response);
        }
    }

    /// Redirects once to <paramref name="redirectTo"/>, then serves <paramref name="finalHtml"/>.
    private sealed class RedirectThenContentHandler(Uri redirectTo, string finalHtml) : HttpMessageHandler
    {
        public int CallCount { get; private set; }

        protected override Task<HttpResponseMessage> SendAsync(
            HttpRequestMessage request, CancellationToken cancellationToken)
        {
            CallCount++;

            if (request.RequestUri == redirectTo)
            {
                return Task.FromResult(new HttpResponseMessage(HttpStatusCode.OK)
                {
                    Content = new StringContent(finalHtml, Encoding.UTF8, "text/html")
                });
            }

            var response = new HttpResponseMessage(HttpStatusCode.Found);
            response.Headers.Location = redirectTo;
            return Task.FromResult(response);
        }
    }

    private sealed class OversizedContentLengthHandler : HttpMessageHandler
    {
        public TrackingHttpContent Content { get; } = new(5_000_000);

        protected override Task<HttpResponseMessage> SendAsync(
            HttpRequestMessage request, CancellationToken cancellationToken)
            => Task.FromResult(new HttpResponseMessage(HttpStatusCode.OK) { Content = Content });
    }

    private sealed class UnboundedBodyHandler : HttpMessageHandler
    {
        public UnboundedHttpContent Content { get; } = new();

        protected override Task<HttpResponseMessage> SendAsync(
            HttpRequestMessage request, CancellationToken cancellationToken)
            => Task.FromResult(new HttpResponseMessage(HttpStatusCode.OK) { Content = Content });
    }

    /// Reports a large Content-Length up front without any body ever being read — used to
    /// prove the size cap is enforced before the response stream is touched.
    private sealed class TrackingHttpContent : HttpContent
    {
        private readonly long _reportedLength;

        public TrackingHttpContent(long reportedLength)
        {
            _reportedLength = reportedLength;
            Headers.ContentType = new MediaTypeHeaderValue("text/html");
        }

        public bool WasStreamRead { get; private set; }

        protected override Task SerializeToStreamAsync(System.IO.Stream stream, TransportContext? context)
            => Task.CompletedTask;

        protected override bool TryComputeLength(out long length)
        {
            length = _reportedLength;
            return true;
        }

        protected override Task<System.IO.Stream> CreateContentReadStreamAsync()
        {
            WasStreamRead = true;
            return Task.FromResult<System.IO.Stream>(new MemoryStream());
        }
    }

    /// Serves an endless byte stream with no Content-Length header — used to prove the service
    /// abandons reading rather than buffering an unbounded/streamed response fully.
    private sealed class UnboundedHttpContent : HttpContent
    {
        public InfiniteStream Stream { get; } = new();

        public UnboundedHttpContent() => Headers.ContentType = new MediaTypeHeaderValue("text/html");

        protected override Task SerializeToStreamAsync(System.IO.Stream stream, TransportContext? context)
            => throw new NotSupportedException("Not used by these tests.");

        protected override bool TryComputeLength(out long length)
        {
            length = 0;
            return false;
        }

        protected override Task<System.IO.Stream> CreateContentReadStreamAsync()
            => Task.FromResult<System.IO.Stream>(Stream);
    }

    private sealed class InfiniteStream : System.IO.Stream
    {
        public long TotalBytesProduced { get; private set; }

        public override bool CanRead => true;
        public override bool CanSeek => false;
        public override bool CanWrite => false;
        public override long Length => throw new NotSupportedException();

        public override long Position
        {
            get => throw new NotSupportedException();
            set => throw new NotSupportedException();
        }

        public override int Read(byte[] buffer, int offset, int count)
        {
            Array.Fill(buffer, (byte)'a', offset, count);
            TotalBytesProduced += count;
            return count;
        }

        public override Task<int> ReadAsync(byte[] buffer, int offset, int count, CancellationToken cancellationToken)
            => Task.FromResult(Read(buffer, offset, count));

        public override void Flush() { }
        public override long Seek(long offset, SeekOrigin origin) => throw new NotSupportedException();
        public override void SetLength(long value) => throw new NotSupportedException();
        public override void Write(byte[] buffer, int offset, int count) => throw new NotSupportedException();
    }
}
