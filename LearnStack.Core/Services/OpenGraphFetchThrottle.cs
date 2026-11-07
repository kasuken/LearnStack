namespace LearnStack.Services;

/// <summary>
/// Bounds how many outbound metadata fetches may be in flight at once, per user and overall,
/// so a single account cannot saturate the server's outbound HTTP capacity.
/// </summary>
public interface IOpenGraphFetchThrottle
{
    /// <summary>
    /// Tries to reserve a fetch slot for <paramref name="userId"/>. Returns null when the user
    /// or the server is at its limit; otherwise a lease that must be disposed to free the slot.
    /// </summary>
    IDisposable? TryAcquire(string userId);
}

public sealed class OpenGraphFetchThrottle(int maxPerUser = 2, int maxGlobal = 16) : IOpenGraphFetchThrottle
{
    private readonly object _gate = new();
    private readonly Dictionary<string, int> _perUser = new(StringComparer.Ordinal);
    private int _global;

    public IDisposable? TryAcquire(string userId)
    {
        ArgumentException.ThrowIfNullOrEmpty(userId);

        lock (_gate)
        {
            _perUser.TryGetValue(userId, out var current);
            if (current >= maxPerUser || _global >= maxGlobal) return null;

            _perUser[userId] = current + 1;
            _global++;
        }

        return new Lease(this, userId);
    }

    private void Release(string userId)
    {
        lock (_gate)
        {
            _global--;
            if (_perUser.TryGetValue(userId, out var current))
            {
                if (current <= 1) _perUser.Remove(userId);
                else _perUser[userId] = current - 1;
            }
        }
    }

    private sealed class Lease(OpenGraphFetchThrottle owner, string userId) : IDisposable
    {
        private int _disposed;

        public void Dispose()
        {
            if (Interlocked.Exchange(ref _disposed, 1) == 0) owner.Release(userId);
        }
    }
}
