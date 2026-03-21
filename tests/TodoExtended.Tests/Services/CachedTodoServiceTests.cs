using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.Logging.Abstractions;
using Microsoft.Extensions.Options;
using NSubstitute;
using TodoExtended.Web.Data;
using TodoExtended.Web.Services;

namespace TodoExtended.Tests.Services;

/// <summary>
/// Integration tests for <see cref="CachedTodoService"/> using a real SQLite database
/// to verify behaviour that depends on EF Core change tracking and SQL queries.
/// </summary>
public class CachedTodoServiceTests : IDisposable
{
    private readonly string _dbPath;
    private readonly SimpleDbContextFactory _factory;

    public CachedTodoServiceTests()
    {
        _dbPath = Path.Combine(Path.GetTempPath(), $"test-cachedtodo-{Guid.NewGuid():N}.db");
        _factory = new SimpleDbContextFactory($"Data Source={_dbPath}", new EnableForeignKeysInterceptor());

        using var seed = _factory.CreateDbContext();
        seed.Database.EnsureCreated();
    }

    public void Dispose()
    {
        if (File.Exists(_dbPath))
            File.Delete(_dbPath);
    }

    // ── Helpers ────────────────────────────────────────────────────────────────

    private CachedTodoService CreateService(IGraphTodoClient graphClient, IUserTimeZoneService userTimeZone)
    {
        var graphService = new GraphTodoService(graphClient, userTimeZone, NullLogger<GraphTodoService>.Instance);
        var options = Options.Create(new TodoCacheOptions
        {
            StalenessThresholdMinutes = 0, // always stale so tests trigger a sync
            MaxParallelListSync = 4,
        });

        return new CachedTodoService(
            graphService,
            graphClient,
            _factory,
            options,
            userTimeZone,
            NullLogger<CachedTodoService>.Instance);
    }

    /// <summary>
    /// Returns a mock graph client that answers all delta queries with empty pages,
    /// so no new tasks are returned from the server during the sync.
    /// </summary>
    private static IGraphTodoClient EmptyDeltaGraphClient()
    {
        var graphClient = Substitute.For<IGraphTodoClient>();

        graphClient
            .GetListsDeltaPageAsync(Arg.Any<string?>())
            .Returns(new GraphDeltaPage<Microsoft.Graph.Models.TodoTaskList>(
                [], null, "https://graph.example.com/deltalink/lists"));

        graphClient
            .GetTasksDeltaBatchAsync(Arg.Any<IReadOnlyList<(string, string?)>>())
            .Returns(ci =>
            {
                var requests = ci.Arg<IReadOnlyList<(string ListId, string? DeltaOrNextLink)>>();
                IReadOnlyDictionary<string, GraphDeltaPage<Microsoft.Graph.Models.TodoTask>> result =
                    requests.ToDictionary(
                        r => r.ListId,
                        _ => new GraphDeltaPage<Microsoft.Graph.Models.TodoTask>(
                            [], null, "https://graph.example.com/deltalink/tasks"));
                return Task.FromResult(result);
            });

        return graphClient;
    }

    // ── Tests ──────────────────────────────────────────────────────────────────

    /// <summary>
    /// When delta sync runs and the CachedTags table is empty for the user but cached
    /// tasks with tag tokens exist, tags must be rebuilt from those tasks' titles.
    /// This covers the scenario where the tags table was cleared (e.g. after a migration)
    /// while the task cache is still warm, so unchanged tasks would never have their
    /// tags re-extracted through the normal delta path.
    /// </summary>
    [Fact]
    public async Task DeltaSync_WhenTagsTableIsEmpty_RebuildsTagsFromCachedTasks()
    {
        // Arrange: seed a warm cache with task lists and tasks (tags table empty)
        const string userId = "user-1";
        const string listId = "list-1";
        var now = DateTime.UtcNow;

        using (var db = _factory.CreateDbContext())
        {
            db.Users.Add(new User
            {
                Id = userId,
                Email = "test@example.com",
                DisplayName = "Test User",
                CreatedUtc = now,
                LastSeenUtc = now,
            });

            db.CachedTaskLists.Add(new CachedTaskList
            {
                Id = listId,
                DisplayName = "Test List",
                IsSynced = true,
                DeltaToken = "delta:existing",
                LastSyncUtc = DateTime.MinValue, // stale → triggers delta sync
                CreatedUtc = now,
                UpdatedUtc = now,
                UserId = userId,
            });

            db.CachedTasks.AddRange(
                new CachedTask
                {
                    Id = "task-1",
                    ListId = listId,
                    Title = "Buy groceries #shopping",
                    IsCompleted = false,
                    IsDeleted = false,
                    LastSyncUtc = now,
                    CreatedUtc = now,
                    UpdatedUtc = now,
                    UserId = userId,
                },
                new CachedTask
                {
                    Id = "task-2",
                    ListId = listId,
                    Title = "Fix the bug #work #urgent",
                    IsCompleted = false,
                    IsDeleted = false,
                    LastSyncUtc = now,
                    CreatedUtc = now,
                    UpdatedUtc = now,
                    UserId = userId,
                },
                new CachedTask
                {
                    Id = "task-3",
                    ListId = listId,
                    Title = "Task without any tags",
                    IsCompleted = false,
                    IsDeleted = false,
                    LastSyncUtc = now,
                    CreatedUtc = now,
                    UpdatedUtc = now,
                    UserId = userId,
                });

            await db.SaveChangesAsync();
        }

        var userTimeZone = Substitute.For<IUserTimeZoneService>();
        var service = CreateService(EmptyDeltaGraphClient(), userTimeZone);

        // Act: trigger a sync via a public read method (cache is stale + lists exist → delta sync)
        await service.SearchTasksAsync("groceries", userId);

        // Assert: all three distinct tags must have been extracted and stored
        using var assertDb = _factory.CreateDbContext();
        var tagNames = await assertDb.CachedTags
            .Where(t => t.UserId == userId)
            .Select(t => t.Name)
            .OrderBy(n => n)
            .ToListAsync();

        Assert.Equal(["shopping", "urgent", "work"], tagNames);
    }

    /// <summary>
    /// Tags must be associated with the tasks they were extracted from,
    /// so that tag-based task lookup returns the correct tasks.
    /// </summary>
    [Fact]
    public async Task DeltaSync_WhenTagsTableIsEmpty_AssociatesTagsWithTheirTasks()
    {
        // Arrange
        const string userId = "user-2";
        const string listId = "list-2";
        var now = DateTime.UtcNow;

        using (var db = _factory.CreateDbContext())
        {
            db.Users.Add(new User
            {
                Id = userId,
                Email = "user2@example.com",
                DisplayName = "User Two",
                CreatedUtc = now,
                LastSeenUtc = now,
            });

            db.CachedTaskLists.Add(new CachedTaskList
            {
                Id = listId,
                DisplayName = "Work List",
                IsSynced = true,
                DeltaToken = "delta:existing",
                LastSyncUtc = DateTime.MinValue,
                CreatedUtc = now,
                UpdatedUtc = now,
                UserId = userId,
            });

            db.CachedTasks.AddRange(
                new CachedTask
                {
                    Id = "task-a",
                    ListId = listId,
                    Title = "Write report #work",
                    IsCompleted = false,
                    IsDeleted = false,
                    LastSyncUtc = now,
                    CreatedUtc = now,
                    UpdatedUtc = now,
                    UserId = userId,
                },
                new CachedTask
                {
                    Id = "task-b",
                    ListId = listId,
                    Title = "Code review #work",
                    IsCompleted = false,
                    IsDeleted = false,
                    LastSyncUtc = now,
                    CreatedUtc = now,
                    UpdatedUtc = now,
                    UserId = userId,
                });

            await db.SaveChangesAsync();
        }

        var userTimeZone = Substitute.For<IUserTimeZoneService>();
        var service = CreateService(EmptyDeltaGraphClient(), userTimeZone);

        // Act
        await service.SearchTasksAsync("report", userId);

        // Assert: both tasks are linked to the #work tag
        using var assertDb = _factory.CreateDbContext();
        var workTag = await assertDb.CachedTags
            .Include(t => t.Tasks)
            .FirstOrDefaultAsync(t => t.UserId == userId && t.Name == "work");

        Assert.NotNull(workTag);
        var linkedTaskIds = workTag!.Tasks.Select(t => t.Id).OrderBy(id => id).ToList();
        Assert.Equal(["task-a", "task-b"], linkedTaskIds);
    }

    /// <summary>
    /// When the tags table already has entries the rebuild must not run,
    /// leaving existing data untouched.
    /// </summary>
    [Fact]
    public async Task DeltaSync_WhenTagsTableAlreadyHasEntries_DoesNotRebuildTags()
    {
        // Arrange: seed a warm cache where the tags table is already populated
        const string userId = "user-3";
        const string listId = "list-3";
        var now = DateTime.UtcNow;

        using (var db = _factory.CreateDbContext())
        {
            db.Users.Add(new User
            {
                Id = userId,
                Email = "user3@example.com",
                DisplayName = "User Three",
                CreatedUtc = now,
                LastSeenUtc = now,
            });

            db.CachedTaskLists.Add(new CachedTaskList
            {
                Id = listId,
                DisplayName = "List",
                IsSynced = true,
                DeltaToken = "delta:existing",
                LastSyncUtc = DateTime.MinValue,
                CreatedUtc = now,
                UpdatedUtc = now,
                UserId = userId,
            });

            var task = new CachedTask
            {
                Id = "task-x",
                ListId = listId,
                Title = "Some task #existing",
                IsCompleted = false,
                IsDeleted = false,
                LastSyncUtc = now,
                CreatedUtc = now,
                UpdatedUtc = now,
                UserId = userId,
            };
            db.CachedTasks.Add(task);

            // Pre-seed tag with a custom flag so we can detect if it was replaced
            var tag = new CachedTag { Name = "existing", UserId = userId, IsPinned = true };
            db.CachedTags.Add(tag);
            task.Tags.Add(tag);

            await db.SaveChangesAsync();
        }

        var userTimeZone = Substitute.For<IUserTimeZoneService>();
        var service = CreateService(EmptyDeltaGraphClient(), userTimeZone);

        // Act
        await service.SearchTasksAsync("some task", userId);

        // Assert: the pre-existing tag row must still be present and pinned
        using var assertDb = _factory.CreateDbContext();
        var existingTag = await assertDb.CachedTags.FindAsync("existing", userId);

        Assert.NotNull(existingTag);
        Assert.True(existingTag!.IsPinned, "Pre-existing pinned tag should not have been replaced");
    }
}
