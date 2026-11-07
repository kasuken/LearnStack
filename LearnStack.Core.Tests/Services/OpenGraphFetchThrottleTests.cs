using LearnStack.Services;

namespace LearnStack.Core.Tests.Services;

public class OpenGraphFetchThrottleTests
{
    [Fact]
    public void TryAcquire_WhenUserIsAtLimit_ReturnsNullUntilALeaseIsReleased()
    {
        var throttle = new OpenGraphFetchThrottle(maxPerUser: 2, maxGlobal: 10);

        var first = throttle.TryAcquire("u1");
        var second = throttle.TryAcquire("u1");

        Assert.NotNull(first);
        Assert.NotNull(second);
        Assert.Null(throttle.TryAcquire("u1"));

        first!.Dispose();
        first.Dispose(); // double dispose must not free a second slot
        Assert.NotNull(throttle.TryAcquire("u1"));
        Assert.Null(throttle.TryAcquire("u1"));
    }

    [Fact]
    public void TryAcquire_OneUserAtLimit_DoesNotBlockAnotherUser()
    {
        var throttle = new OpenGraphFetchThrottle(maxPerUser: 1, maxGlobal: 10);
        using var a = throttle.TryAcquire("u1");

        Assert.Null(throttle.TryAcquire("u1"));
        Assert.NotNull(throttle.TryAcquire("u2"));
    }

    [Fact]
    public void TryAcquire_WhenGlobalLimitReached_RejectsEveryone()
    {
        var throttle = new OpenGraphFetchThrottle(maxPerUser: 5, maxGlobal: 2);
        using var a = throttle.TryAcquire("u1");
        using var b = throttle.TryAcquire("u2");

        Assert.Null(throttle.TryAcquire("u3"));
    }
}
