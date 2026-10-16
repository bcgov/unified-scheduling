using Unified.Calendar.Services;
using Unified.Db.Models.Calendar;
using Unified.Db.Models.Leave;
using Unified.Leave.Models;
using Unified.Leave.Services;

namespace Unified.Tests.Leave.Services;

public sealed class SchedulingLeaveSeriesFacadeTests : IAsyncLifetime
{
    private LeaveTestDatabase _database = null!;
    private readonly FakeMaterializer _materializer = new();
    private SchedulingLeaveSeriesFacade _facade = null!;

    public async ValueTask InitializeAsync()
    {
        _database = await LeaveTestDatabase.CreateAsync();
        _facade = new SchedulingLeaveSeriesFacade(_materializer, new LeaveSeriesMaterializationHandler(_database.Db));
    }

    public async ValueTask DisposeAsync() => await _database.DisposeAsync();

    [Fact]
    public async Task MaterializeSeriesAsync_BuildsContextFromSuppliedSeries()
    {
        var series = new LeaveSeries { EventSeries = new EventSeries { Title = "Leave" } };

        Guid[] userIds = [Guid.NewGuid()];

        await _facade.MaterializeSeriesAsync(series, userIds, TestContext.Current.CancellationToken);

        var context = Assert.IsType<LeaveSeriesMaterializationContext>(_materializer.Context);
        Assert.Same(userIds, context.UserIds);
        Assert.Same(series, context.LeaveSeries);
        Assert.Same(series.EventSeries, _materializer.EventSeries);
        Assert.Empty(context.ExistingEntries);
        Assert.False(_materializer.Regenerated);
        Assert.Equal(TestContext.Current.CancellationToken, _materializer.CancellationToken);
    }

    [Fact]
    public async Task RegenerateSeriesAsync_PassesExplicitLoadedEntriesAndSuppliedSeries()
    {
        var series = new LeaveSeries { EventSeries = new EventSeries { Title = "Leave" } };
        LeaveEntry[] entries = [new LeaveEntry { LeaveSeries = series }];

        Guid[] userIds = [Guid.NewGuid()];

        await _facade.RegenerateSeriesAsync(series, userIds, entries, TestContext.Current.CancellationToken);

        var context = Assert.IsType<LeaveSeriesMaterializationContext>(_materializer.Context);
        Assert.Same(userIds, context.UserIds);
        Assert.Same(series, context.LeaveSeries);
        Assert.Same(series.EventSeries, _materializer.EventSeries);
        Assert.Same(entries, context.ExistingEntries);
        Assert.True(_materializer.Regenerated);
        Assert.Equal(TestContext.Current.CancellationToken, _materializer.CancellationToken);
    }

    [Fact]
    public async Task MaterializeSeriesAsync_WhenCalendarSeriesMissing_Throws()
    {
        await Assert.ThrowsAsync<InvalidOperationException>(() =>
            _facade.MaterializeSeriesAsync(new LeaveSeries(), [], TestContext.Current.CancellationToken)
        );
        Assert.Null(_materializer.Context);
    }

    [Fact]
    public async Task RegenerateSeriesAsync_WhenLoadedEntriesNull_Throws()
    {
        await Assert.ThrowsAsync<ArgumentNullException>(() =>
            _facade.RegenerateSeriesAsync(new LeaveSeries(), [], null!, TestContext.Current.CancellationToken)
        );
        Assert.Null(_materializer.Context);
    }

    private sealed class FakeMaterializer : IEventSeriesMaterializationService
    {
        public EventSeries? EventSeries { get; private set; }
        public object? Context { get; private set; }
        public bool Regenerated { get; private set; }
        public CancellationToken CancellationToken { get; private set; }

        public Task MaterializeAsync<TContext>(
            EventSeries eventSeries,
            RecurrenceValidationOptions validationOptions,
            IEventSeriesMaterializationHandler<TContext> handler,
            TContext context,
            CancellationToken cancellationToken
        )
            where TContext : notnull
        {
            EventSeries = eventSeries;
            Context = context;
            CancellationToken = cancellationToken;
            return Task.CompletedTask;
        }

        public Task RegenerateDraftSeriesAsync<TContext>(
            EventSeries eventSeries,
            RecurrenceValidationOptions validationOptions,
            IEventSeriesMaterializationHandler<TContext> handler,
            TContext context,
            CancellationToken cancellationToken
        )
            where TContext : notnull
        {
            Regenerated = true;
            return MaterializeAsync(eventSeries, validationOptions, handler, context, cancellationToken);
        }
    }
}
