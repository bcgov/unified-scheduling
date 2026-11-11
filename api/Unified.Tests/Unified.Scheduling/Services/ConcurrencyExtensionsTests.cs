using Microsoft.EntityFrameworkCore;
using Unified.Db;
using Unified.Db.Extensions;
using Unified.Db.Models;
using Unified.Db.Models.Calendar;
using Unified.Db.Models.Scheduling;
using Unified.Scheduling;
using Unified.Scheduling.Interceptors;

namespace Unified.Tests.Scheduling.Services;

public sealed class ConcurrencyExtensionsTests
{
    [Theory]
    [InlineData(EntityState.Added)]
    [InlineData(EntityState.Deleted)]
    [InlineData(EntityState.Detached)]
    public void TouchForConcurrency_WhenRootIsNotAnExistingUpdate_DoesNotChangeItsState(EntityState state)
    {
        using var db = CreateContext();
        var root = new ShiftEntry { Id = 1, ConcurrencyToken = 7 };
        db.Entry(root).State = state;

        db.TouchForConcurrency(root);

        Assert.Equal(state, db.Entry(root).State);
        Assert.Null(root.UpdatedOn);
    }

    [Fact]
    public void TouchForConcurrency_WhenRootAlreadyHasAClientCheck_PreservesTheOriginalVersion()
    {
        using var db = CreateContext();
        var root = new ShiftEntry { Id = 1, ConcurrencyToken = 7 };
        db.Attach(root);
        db.RequireVersion(root, 7);
        var updatedOn = root.UpdatedOn;

        db.TouchForConcurrency(root);

        Assert.Equal(updatedOn, root.UpdatedOn);
        Assert.Equal(7u, db.Entry(root).Property(item => item.ConcurrencyToken).OriginalValue);
    }

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
        var entity = new ShiftEntry
        {
            Id = 1,
            EventId = 2,
            ConcurrencyToken = 7,
        };
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
            .AddInterceptors(new SchedulingConcurrencyInterceptor())
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

    [Theory]
    [InlineData(false)]
    [InlineData(true)]
    public async Task SaveChanges_WhenEmployeeIsReassigned_TouchesBothOccurrencesAndSeries(bool asyncSave)
    {
        var ct = TestContext.Current.CancellationToken;
        await using var db = CreateContext();
        var oldShift = new ShiftEntry
        {
            ShiftSeries = new ShiftSeries(),
            Users = [new ShiftEntryUser { UserId = Guid.NewGuid() }],
        };
        var newShift = new ShiftEntry { ShiftSeries = new ShiftSeries() };
        db.AddRange(oldShift, newShift);
        await db.SaveChangesAsync(ct);
        Assert.Null(oldShift.UpdatedOn);
        Assert.Null(newShift.UpdatedOn);
        db.ChangeTracker.Clear();

        var employee = await db.ShiftEntryUsers.SingleAsync(ct);
        employee.ShiftEntryId = newShift.Id;
        if (asyncSave)
            await db.SaveChangesAsync(ct);
        else
            db.SaveChanges();

        Assert.All(await db.ShiftEntries.ToListAsync(ct), root => Assert.NotNull(root.UpdatedOn));
        Assert.All(await db.ShiftSeries.ToListAsync(ct), root => Assert.NotNull(root.UpdatedOn));
    }

    [Theory]
    [InlineData(false)]
    [InlineData(true)]
    public async Task SaveChanges_WhenAssignedUserMovesBetweenUnloadedLinks_TouchesBothOwners(bool series)
    {
        var ct = TestContext.Current.CancellationToken;
        await using var db = CreateContext();
        var assignment = new AssignmentEntry { AssignmentSeries = new AssignmentSeries() };
        var previous = CreateLink();
        var replacement = CreateLink();
        var userId = Guid.NewGuid();
        if (series)
            previous.ShiftAssignmentSeriesLink!.Users.Add(new ShiftAssignmentSeriesLinkUser { UserId = userId });
        else
            previous.Users.Add(new ShiftAssignmentEntryUser { UserId = userId });
        db.AddRange(previous, replacement);
        await db.SaveChangesAsync(ct);
        db.ChangeTracker.Clear();

        if (series)
            (await db.ShiftAssignmentSeriesLinkUsers.SingleAsync(ct)).ShiftAssignmentSeriesLinkId = replacement
                .ShiftAssignmentSeriesLink!
                .Id;
        else
            (await db.ShiftAssignmentEntryUsers.SingleAsync(ct)).ShiftAssignmentEntryId = replacement.Id;
        await db.SaveChangesAsync(ct);

        Assert.All(await db.ShiftEntries.ToListAsync(ct), root => Assert.Equal(!series, root.UpdatedOn.HasValue));
        Assert.Equal(!series, (await db.AssignmentEntries.SingleAsync(ct)).UpdatedOn.HasValue);
        Assert.All(await db.ShiftSeries.ToListAsync(ct), root => Assert.NotNull(root.UpdatedOn));
        Assert.NotNull((await db.AssignmentSeries.SingleAsync(ct)).UpdatedOn);

        ShiftAssignmentEntry CreateLink()
        {
            var shift = new ShiftEntry { ShiftSeries = new ShiftSeries() };
            return new ShiftAssignmentEntry
            {
                ShiftEntry = shift,
                AssignmentEntry = assignment,
                ShiftAssignmentSeriesLink = new ShiftAssignmentSeriesLink
                {
                    ShiftSeries = shift.ShiftSeries,
                    AssignmentSeries = assignment.AssignmentSeries,
                },
            };
        }
    }

    [Theory]
    [InlineData(false)]
    [InlineData(true)]
    public async Task SaveChanges_WhenSeriesDeletionNullsUnloadedChildren_TouchesSurvivingOccurrences(bool asyncSave)
    {
        var ct = TestContext.Current.CancellationToken;
        await using var db = CreateContext();
        var shift = new ShiftEntry { ShiftSeries = new ShiftSeries() };
        var assignment = new AssignmentEntry { AssignmentSeries = new AssignmentSeries() };
        db.AddRange(shift, assignment);
        await db.SaveChangesAsync(ct);
        db.ChangeTracker.Clear();
        var shiftSeries = await db.ShiftSeries.SingleAsync(ct);
        var assignmentSeries = await db.AssignmentSeries.SingleAsync(ct);
        db.RemoveRange(shiftSeries, assignmentSeries);

        if (asyncSave)
            await db.SaveChangesAsync(ct);
        else
            db.SaveChanges();

        var retainedShift = await db.ShiftEntries.SingleAsync(ct);
        var retainedAssignment = await db.AssignmentEntries.SingleAsync(ct);
        Assert.Null(retainedShift.ShiftSeriesId);
        Assert.Null(retainedAssignment.AssignmentSeriesId);
        Assert.NotNull(retainedShift.UpdatedOn);
        Assert.NotNull(retainedAssignment.UpdatedOn);
        Assert.Null(shiftSeries.UpdatedOn);
        Assert.Null(assignmentSeries.UpdatedOn);
    }

    [Theory]
    [InlineData("scheduling", "shift", true)]
    [InlineData("scheduling", "assignment", true)]
    [InlineData("training", "training", false)]
    [InlineData("leave", "leave", false)]
    [InlineData("loanout", "loanout", false)]
    [InlineData("training", "shift", false)]
    [InlineData("scheduling", "general", false)]
    public async Task SaveChanges_WhenCalendarEventChanges_OnlyTouchesSchedulingOwnedTypes(
        string sourceModule,
        string eventType,
        bool expectedTouch
    )
    {
        var ct = TestContext.Current.CancellationToken;
        await using var db = CreateContext();
        var calendarEvent = new Event { SourceModule = sourceModule, EventTypeCode = eventType };
        var shift = new ShiftEntry { Event = calendarEvent, ShiftSeries = new ShiftSeries() };
        var assignment = new AssignmentEntry { Event = calendarEvent, AssignmentSeries = new AssignmentSeries() };
        db.AddRange(shift, assignment);
        await db.SaveChangesAsync(ct);
        db.ChangeTracker.Clear();

        (await db.Events.SingleAsync(ct)).Title = "Changed";
        await db.SaveChangesAsync(ct);

        Assert.Equal(
            expectedTouch && eventType == SchedulingConstants.ShiftEventTypeCode,
            (await db.ShiftEntries.SingleAsync(ct)).UpdatedOn.HasValue
        );
        Assert.Equal(
            expectedTouch && eventType == SchedulingConstants.AssignmentEventTypeCode,
            (await db.AssignmentEntries.SingleAsync(ct)).UpdatedOn.HasValue
        );
        Assert.Equal(
            expectedTouch && eventType == SchedulingConstants.ShiftEventTypeCode,
            (await db.ShiftSeries.SingleAsync(ct)).UpdatedOn.HasValue
        );
        Assert.Equal(
            expectedTouch && eventType == SchedulingConstants.AssignmentEventTypeCode,
            (await db.AssignmentSeries.SingleAsync(ct)).UpdatedOn.HasValue
        );
    }

    [Fact]
    public async Task SaveChanges_WhenOnlyAuditRecordOrUnchangedEmployeeIsPending_DoesNotRetouchRoots()
    {
        var ct = TestContext.Current.CancellationToken;
        await using var db = CreateContext();
        // Other test classes can enable Audit.NET globally; this test stages its own audit record.
        db.AuditDisabled = true;
        var shift = new ShiftEntry { Users = [new ShiftEntryUser { UserId = Guid.NewGuid() }] };
        db.Add(shift);
        await db.SaveChangesAsync(ct);
        shift.Users.Add(new ShiftEntryUser { UserId = Guid.NewGuid() });
        await db.SaveChangesAsync(ct);
        var touchedOn = shift.UpdatedOn;
        Assert.NotNull(touchedOn);

        var employee = shift.Users.First();
        db.Entry(employee).Property(item => item.UserId).IsModified = true;
        db.AuditRecords.Add(new AuditRecord { Action = "Modified", EntityType = nameof(ShiftEntry) });
        await db.SaveChangesAsync(ct);
        Assert.Equal(touchedOn, shift.UpdatedOn);
        Assert.Equal(0, await db.SaveChangesAsync(ct));
        Assert.Single(await db.AuditRecords.ToListAsync(ct));
    }

    private static UnifiedDbContext CreateContext() =>
        new TestContextDb(
            new DbContextOptionsBuilder<UnifiedDbContext>()
                .UseInMemoryDatabase(Guid.NewGuid().ToString())
                .AddInterceptors(new SchedulingConcurrencyInterceptor())
                .Options
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
