using Microsoft.EntityFrameworkCore;
using Unified.Db;
using Unified.Db.Extensions;
using Unified.Db.Models.Scheduling;

namespace Unified.Tests.Scheduling.Services;

public sealed class ConcurrencyExtensionsTests
{
    [Fact]
    public void RequireVersion_WhenDetached_RejectsWithoutAttaching()
    {
        using var db = CreateContext();
        var entity = new ShiftEntry { ConcurrencyToken = 7 };

        Assert.Throws<InvalidOperationException>(() => db.RequireVersion(entity, 7));

        Assert.Equal(EntityState.Detached, db.Entry(entity).State);
    }

    [Fact]
    public void RequireVersion_WhenStale_ThrowsBeforeModifyingTrackedState()
    {
        using var db = CreateContext();
        var entity = new ShiftEntry { Id = 1, ConcurrencyToken = 7 };
        db.Attach(entity);

        Assert.Throws<DbUpdateConcurrencyException>(() => db.RequireVersion(entity, 6));

        Assert.Equal(EntityState.Unchanged, db.Entry(entity).State);
        Assert.Null(entity.UpdatedOn);
        Assert.Equal(7u, db.Entry(entity).Property(item => item.ConcurrencyToken).OriginalValue);
    }

    [Theory]
    [InlineData(false)]
    [InlineData(true)]
    public async Task RequireVersion_WhenOnlyRootTouchIsNeeded_ParticipatesInSyncAndAsyncSaves(bool asyncSave)
    {
        await using var db = CreateContext();
        var entity = new ShiftEntry { Id = 1, EventId = 2, ConcurrencyToken = 7 };
        db.Add(entity);
        await db.SaveChangesAsync(TestContext.Current.CancellationToken);

        entity.EventId = 3;
        db.RequireVersion(entity, 7);

        var entry = db.Entry(entity);
        Assert.Equal(2, entry.Property(item => item.EventId).OriginalValue);
        Assert.True(entry.Property(item => item.UpdatedOn).IsModified);
        if (asyncSave)
            await db.SaveChangesAsync(TestContext.Current.CancellationToken);
        else
            db.SaveChanges();

        // A second save must use EF's accepted original values, without reapplying
        // the client's old token. PostgreSQL supplies the new xmin via RETURNING.
        Assert.Equal(EntityState.Unchanged, entry.State);
        Assert.Equal(entity.ConcurrencyToken, entry.Property(item => item.ConcurrencyToken).OriginalValue);
        db.RequireVersion(entity, entity.ConcurrencyToken);
        Assert.True(entry.Property(item => item.UpdatedOn).IsModified);
    }

    [Fact]
    public async Task RequireVersion_WhenWriterChangesAfterLoad_EfRejectsTheSave()
    {
        var options = new DbContextOptionsBuilder<UnifiedDbContext>()
            .UseInMemoryDatabase(Guid.NewGuid().ToString())
            .Options;
        await using var first = new TestContextDb(options);
        first.Add(new ShiftEntry { Id = 1, ConcurrencyToken = 7 });
        await first.SaveChangesAsync(TestContext.Current.CancellationToken);
        var stale = await first.ShiftEntries.SingleAsync(TestContext.Current.CancellationToken);
        first.RequireVersion(stale, 7);

        await using var second = new TestContextDb(options);
        var current = await second.ShiftEntries.SingleAsync(TestContext.Current.CancellationToken);
        // Simulates PostgreSQL advancing xmin; the in-memory provider cannot generate it.
        current.ConcurrencyToken = 8;
        await second.SaveChangesAsync(TestContext.Current.CancellationToken);

        await Assert.ThrowsAsync<DbUpdateConcurrencyException>(() =>
            first.SaveChangesAsync(TestContext.Current.CancellationToken)
        );
    }

    private static UnifiedDbContext CreateContext() =>
        new TestContextDb(
            new DbContextOptionsBuilder<UnifiedDbContext>().UseInMemoryDatabase(Guid.NewGuid().ToString()).Options
        );

    private sealed class TestContextDb(DbContextOptions<UnifiedDbContext> options) : UnifiedDbContext(options)
    {
        protected override void OnModelCreating(ModelBuilder modelBuilder)
        {
            modelBuilder.ApplyAllConfigurations(typeof(UnifiedDbContext).Assembly, this);
            modelBuilder.Entity<ShiftEntry>().Property(item => item.ConcurrencyToken).ValueGeneratedNever();
        }
    }
}
