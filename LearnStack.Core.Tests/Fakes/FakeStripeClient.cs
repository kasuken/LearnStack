using Stripe;

namespace LearnStack.Core.Tests.Fakes;

/// <summary>
/// Test double for <see cref="IStripeClient"/> that never makes a network call: it hands back
/// caller-supplied canned responses keyed by the requested entity type, so
/// <c>StripeBillingProvider</c>'s checkout/portal-session creation can be exercised end-to-end
/// in tests without ever calling the real Stripe API.
/// </summary>
internal sealed class FakeStripeClient(Func<Type, IStripeEntity> respond) : IStripeClient
{
    public List<(HttpMethod Method, string Path)> Requests { get; } = [];

    /// <summary>The options object sent with each request, so tests can assert what Stripe was actually asked for.</summary>
    public List<BaseOptions> SentOptions { get; } = [];

    public string ApiKey => "sk_test_fake";
    public string? ClientId => null;
    public string ApiBase => "https://api.stripe.com";
    public string ConnectBase => "https://connect.stripe.com";
    public string FilesBase => "https://files.stripe.com";
    public string MeterEventsBase => "https://meter-events.stripe.com";

    public Task<T> RequestAsync<T>(
        HttpMethod method,
        string path,
        BaseOptions options,
        RequestOptions? requestOptions,
        CancellationToken cancellationToken = default)
        where T : IStripeEntity
    {
        Requests.Add((method, path));
        SentOptions.Add(options);
        return Task.FromResult((T)respond(typeof(T)));
    }

    public Task<Stream> RequestStreamingAsync(
        HttpMethod method,
        string path,
        BaseOptions options,
        RequestOptions? requestOptions,
        CancellationToken cancellationToken = default) =>
        throw new NotSupportedException("Not used by any code path under test.");
}
