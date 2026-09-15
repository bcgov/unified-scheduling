using Microsoft.EntityFrameworkCore;
using Microsoft.EntityFrameworkCore.Diagnostics;
using Microsoft.Extensions.Logging.Abstractions;
using Unified.Api.Services;
using Unified.Common.Interceptors;
using Unified.Common.PostSave;
using Unified.Common.Seeding;

namespace Unified.Tests.Common.PostSave;

public sealed class PostSaveInterceptorTests
{
    [Theory]
    [InlineData(SaveAction.Create)]
    [InlineData(SaveAction.Update)]
    [InlineData(SaveAction.Delete)]
    public async Task SaveChangesAsync_DispatchesMatchingActionAfterPersistence(SaveAction action)
    {
        var calls = new List<string>();
        var handler = new FakePostSaveHandler(
            typeof(ExampleEntity),
            action,
            async (_, context, token) =>
            {
                var saved = (ExampleEntity)context.Entity;
                Assert.Equal(action, context.Action);
                Assert.Equal(TestContext.Current.CancellationToken, token);
                Assert.NotEqual(0, saved.Id);
                calls.Add("first-start");
                await Task.Yield();
                calls.Add("first-end");
            }
        );
        SaveTestDbContext? db = null;
        var second = new FakePostSaveHandler(
            typeof(ExampleEntity),
            action,
            async (savedDb, context, token) =>
            {
                Assert.Same(db, savedDb);
                Assert.Equal(new[] { "first-start", "first-end" }, calls);
                var persisted = await db!.Set<ExampleEntity>().AsNoTracking().SingleOrDefaultAsync(token);
                if (action == SaveAction.Delete)
                {
                    Assert.Null(persisted);
                    Assert.Equal(EntityState.Detached, db.Entry(context.Entity).State);
                }
                else
                {
                    Assert.Equal("Saved", persisted!.Value);
                    Assert.Equal(EntityState.Unchanged, db.Entry(context.Entity).State);
                }
                calls.Add("second");
                db.Add(new ExampleEntity { Value = "Related" });
            }
        );
        var other = new FakePostSaveHandler(
            typeof(OtherEntity),
            action,
            (_, _, _) => throw new InvalidOperationException("Wrong entity type dispatched.")
        );
        var otherAction = new FakePostSaveHandler(
            typeof(ExampleEntity),
            action == SaveAction.Create ? SaveAction.Update : SaveAction.Create,
            (_, _, _) => throw new InvalidOperationException("Wrong action dispatched.")
        );
        var interceptor = new PostSaveInterceptor([handler, second, other, otherAction], TimeProvider.System);
        await using var context = await CreateDbAsync(interceptor);
        db = context;
        var entity = new ExampleEntity { Value = "Saved" };
        db.Add(entity);
        if (action != SaveAction.Create)
        {
            await db.SaveChangesAsync(TestContext.Current.CancellationToken);
            if (action == SaveAction.Delete)
                db.Remove(entity);
            else
                entity.Value = "Saved" + " update";
            if (action == SaveAction.Update)
            {
                entity.Value = "Saved";
                db.Entry(entity).Property(x => x.Value).IsModified = true;
            }
        }

        await using var transaction = await db.Database.BeginTransactionAsync(TestContext.Current.CancellationToken);
        var result = await db.SaveChangesAsync(TestContext.Current.CancellationToken);
        await transaction.CommitAsync(TestContext.Current.CancellationToken);

        Assert.Equal(1, result);
        Assert.Equal(new[] { "first-start", "first-end", "second" }, calls);
        Assert.Single(
            await db.Set<ExampleEntity>()
                .Where(x => x.Value == "Related")
                .ToListAsync(TestContext.Current.CancellationToken)
        );
        Assert.False(db.ChangeTracker.HasChanges());
    }

    [Fact]
    public async Task UnrelatedSave_DoesNotInvokeHandler()
    {
        var calls = 0;
        var handler = new FakePostSaveHandler(
            typeof(OtherEntity),
            SaveAction.Create,
            (_, _, _) =>
            {
                calls++;
                return Task.CompletedTask;
            }
        );
        var interceptor = new PostSaveInterceptor([handler], TimeProvider.System);
        await using var db = await CreateDbAsync(interceptor);
        db.Add(new ExampleEntity());

        await db.SaveChangesAsync(TestContext.Current.CancellationToken);

        Assert.Equal(0, calls);
        Assert.Single(await db.Set<ExampleEntity>().ToListAsync(TestContext.Current.CancellationToken));
    }

    [Fact]
    public async Task SeederFactory_AllowsHandlersDuringSeeding()
    {
        var calls = 0;
        var handler = new FakePostSaveHandler(
            typeof(ExampleEntity),
            SaveAction.Create,
            (_, _, _) =>
            {
                calls++;
                return Task.CompletedTask;
            }
        );
        await using var db = await CreateDbAsync(new PostSaveInterceptor([handler], TimeProvider.System));
        var factory = new SeederFactory<SaveTestDbContext>(
            NullLogger<SeederFactory<SaveTestDbContext>>.Instance,
            [new TestSeeder(false)]
        );

        await factory.SeedAsync(db, TestContext.Current.CancellationToken);
        Assert.Equal(1, calls);

        db.Add(new ExampleEntity { Value = "Not seeded" });
        await using var transaction = await db.Database.BeginTransactionAsync(TestContext.Current.CancellationToken);
        await db.SaveChangesAsync(TestContext.Current.CancellationToken);
        await transaction.CommitAsync(TestContext.Current.CancellationToken);
        Assert.Equal(2, calls);
    }

    [Fact]
    public async Task SaveChangesWithoutAcceptingChanges_RejectsDispatchRatherThanReplayingOriginalInsert()
    {
        var calls = 0;
        var handler = new FakePostSaveHandler(
            typeof(ExampleEntity),
            SaveAction.Create,
            (_, _, _) =>
            {
                calls++;
                return Task.CompletedTask;
            }
        );
        await using var db = await CreateDbAsync(new PostSaveInterceptor([handler], TimeProvider.System));
        db.Add(new ExampleEntity());
        await using var transaction = await db.Database.BeginTransactionAsync(TestContext.Current.CancellationToken);

        await Assert.ThrowsAsync<InvalidOperationException>(() =>
            db.SaveChangesAsync(false, TestContext.Current.CancellationToken)
        );
        await transaction.RollbackAsync(TestContext.Current.CancellationToken);

        Assert.Equal(0, calls);
        Assert.Empty(await db.Set<ExampleEntity>().AsNoTracking().ToListAsync(TestContext.Current.CancellationToken));
    }

    private static async Task<SaveTestDbContext> CreateDbAsync(IInterceptor interceptor)
    {
        var db = new SaveTestDbContext(
            new DbContextOptionsBuilder<SaveTestDbContext>()
                .UseSqlite("Data Source=:memory:")
                .AddInterceptors(interceptor)
                .Options
        );
        await db.Database.OpenConnectionAsync(TestContext.Current.CancellationToken);
        await db.Database.EnsureCreatedAsync(TestContext.Current.CancellationToken);
        return db;
    }

    private sealed class SaveTestDbContext(DbContextOptions<SaveTestDbContext> options) : DbContext(options)
    {
        protected override void OnModelCreating(ModelBuilder builder)
        {
            builder.Entity<ExampleEntity>();
            builder.Entity<OtherEntity>();
        }
    }

    private sealed class ExampleEntity
    {
        public int Id { get; set; }
        public string Value { get; set; } = string.Empty;
    }

    private sealed class OtherEntity
    {
        public int Id { get; set; }
    }

    private sealed class FakePostSaveHandler(
        Type entityType,
        SaveAction action,
        Func<DbContext, SaveContext, CancellationToken, Task> handleAsync
    ) : IPostSaveHandler
    {
        public Type EntityType => entityType;
        public SaveAction Action => action;

        public Task HandleAsync(DbContext db, SaveContext context, CancellationToken cancellationToken) =>
            handleAsync(db, context, cancellationToken);
    }

    private sealed class TestSeeder(bool fail) : SeederBase<SaveTestDbContext>(NullLogger<TestSeeder>.Instance)
    {
        public override int Order => 0;
        public override string Name => "Test";

        protected override async Task ExecuteAsync(SaveTestDbContext dbContext, CancellationToken cancellationToken)
        {
            dbContext.Add(new ExampleEntity { Value = "Seeded" });
            await dbContext.SaveChangesAsync(cancellationToken);
            if (fail)
                throw new InvalidOperationException("Seeder failed.");
        }
    }
}
