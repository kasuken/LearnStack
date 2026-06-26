using LearnStack.Helpers;

namespace LearnStack.Core.Tests.Helpers;

public class UrlNormalizerTests
{
    [Theory]
    [InlineData(null)]
    [InlineData("")]
    [InlineData("   ")]
    public void Normalize_WithNullOrWhitespace_ReturnsEmpty(string? url)
    {
        Assert.Equal(string.Empty, UrlNormalizer.Normalize(url!));
    }

    [Theory]
    [InlineData("https://EXAMPLE.COM/", "https://example.com")]
    [InlineData("HTTPS://Example.COM/path/", "https://example.com/path")]
    [InlineData("HTTP://Example.com/", "http://example.com")]
    public void Normalize_NormalizesSchemeAndHostToLowercase(string input, string expected)
    {
        Assert.Equal(expected, UrlNormalizer.Normalize(input));
    }

    [Theory]
    [InlineData("https://example.com/path/", "https://example.com/path")]
    [InlineData("https://example.com/path", "https://example.com/path")]
    [InlineData("https://example.com/", "https://example.com")]
    public void Normalize_StripsTrailingSlash(string input, string expected)
    {
        Assert.Equal(expected, UrlNormalizer.Normalize(input));
    }

    [Theory]
    [InlineData("https://example.com/A/B?q=1", "https://example.com/A/B?q=1")]
    public void Normalize_PreservesPathCaseAndQuery(string input, string expected)
    {
        Assert.Equal(expected, UrlNormalizer.Normalize(input));
    }

    [Theory]
    [InlineData("not-a-url", "not-a-url")]
    [InlineData("NOT-A-URL", "not-a-url")]
    [InlineData("not-a-url/", "not-a-url")]
    public void Normalize_WithNonAbsoluteUrl_LowercasesAndStripsSlash(string input, string expected)
    {
        Assert.Equal(expected, UrlNormalizer.Normalize(input));
    }

    [Fact]
    public void Normalize_TwoUrlsDifferingOnlyInSchemeCase_AreEqual()
    {
        var a = UrlNormalizer.Normalize("HTTPS://Example.com/page");
        var b = UrlNormalizer.Normalize("https://example.com/page");
        Assert.Equal(a, b);
    }
}
