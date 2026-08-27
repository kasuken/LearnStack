using LearnStack.Data;
using Microsoft.Data.Sqlite;
using Microsoft.EntityFrameworkCore;

namespace LearnStack.Core.Tests;

/// <summary>
/// Keeps a single SQLite in-memory connection alive for the duration of a test
/// so all context instances within that test share the same database.
/// Dispose after each test to reset state.
/// </summary>
internal sealed class TestDbContextFactory : IDbContextFactory<ApplicationDbContext>, IAsyncDisposable
{
    private readonly SqliteConnection _connection;
    private readonly DbContextOptions<ApplicationDbContext> _options;

    private TestDbContextFactory(SqliteConnection connection, DbContextOptions<ApplicationDbContext> options)
    {
        _connection = connection;
        _options = options;
    }

    /// <summary>
    /// Creates the factory and optionally seeds Identity users so FK constraints are satisfied.
    /// </summary>
    public static async Task<TestDbContextFactory> CreateAsync(params string[] userIdsToSeed)
    {
        var connection = new SqliteConnection("Data Source=:memory:");
        await connection.OpenAsync();

        var options = new DbContextOptionsBuilder<ApplicationDbContext>()
            .UseSqlite(connection)
            .Options;

        await using (var ctx = new ApplicationDbContext(options))
        {
            await ctx.Database.EnsureCreatedAsync();

            foreach (var userId in userIdsToSeed)
            {
                ctx.Users.Add(new ApplicationUser
                {
                    Id = userId,
                    UserName = userId,
                    NormalizedUserName = userId.ToUpperInvariant(),
                    Email = $"{userId}@test.local",
                    NormalizedEmail = $"{userId}@test.local".ToUpperInvariant(),
                    SecurityStamp = Guid.NewGuid().ToString(),
                    ConcurrencyStamp = Guid.NewGuid().ToString()
                });
            }

            if (userIdsToSeed.Length > 0)
                await ctx.SaveChangesAsync();
        }

        return new TestDbContextFactory(connection, options);
    }

    public ApplicationDbContext CreateDbContext() => new(_options);

    public Task<ApplicationDbContext> CreateDbContextAsync(CancellationToken cancellationToken = default)
        => Task.FromResult(CreateDbContext());

    public async ValueTask DisposeAsync()
    {
        await _connection.DisposeAsync();
    }
}
