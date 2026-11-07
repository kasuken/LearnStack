using System.Globalization;
using System.Threading.RateLimiting;
using Microsoft.AspNetCore.RateLimiting;

namespace LearnStack.Extensions;

public static class RateLimitingExtensions
{
    /// <summary>Failed-or-not POSTs to /Account/* (login, register, password reset) per client IP per minute.</summary>
    internal const int AuthPermitsPerMinute = 20;

    /// <summary>Requests to anonymous token pages (/shared/{token}, /friends/accept/{token}) per client IP per minute.</summary>
    internal const int TokenPagePermitsPerMinute = 30;

    /// <summary>
    /// Per-client-IP throttling for the anonymous or credential-guessing surface: account form
    /// posts and token-addressed pages. Over-limit requests receive 429 with Retry-After.
    /// The client IP is the one resolved by the forwarded-headers middleware.
    /// </summary>
    public static IServiceCollection AddLearnStackRateLimiting(this IServiceCollection services)
    {
        services.AddRateLimiter(options =>
        {
            options.RejectionStatusCode = StatusCodes.Status429TooManyRequests;
            options.OnRejected = (context, _) =>
            {
                if (context.Lease.TryGetMetadata(MetadataName.RetryAfter, out var retryAfter))
                {
                    context.HttpContext.Response.Headers.RetryAfter =
                        ((int)Math.Ceiling(retryAfter.TotalSeconds)).ToString(CultureInfo.InvariantCulture);
                }
                return ValueTask.CompletedTask;
            };

            options.GlobalLimiter = PartitionedRateLimiter.Create<HttpContext, string>(context =>
            {
                var path = context.Request.Path;
                var ip = context.Connection.RemoteIpAddress?.ToString() ?? "unknown";

                if (HttpMethods.IsPost(context.Request.Method) && path.StartsWithSegments("/Account"))
                {
                    return Window($"auth:{ip}", AuthPermitsPerMinute);
                }

                if (path.StartsWithSegments("/shared") || path.StartsWithSegments("/friends/accept"))
                {
                    return Window($"token:{ip}", TokenPagePermitsPerMinute);
                }

                return RateLimitPartition.GetNoLimiter("unlimited");
            });
        });

        return services;
    }

    private static RateLimitPartition<string> Window(string key, int permits) =>
        RateLimitPartition.GetFixedWindowLimiter(key, _ => new FixedWindowRateLimiterOptions
        {
            PermitLimit = permits,
            Window = TimeSpan.FromMinutes(1),
            QueueLimit = 0,
        });
}
