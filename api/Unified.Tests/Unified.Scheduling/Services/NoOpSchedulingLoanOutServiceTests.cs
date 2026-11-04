using Unified.Db.Models.LoanOut;
using Unified.Scheduling.Services;

namespace Unified.Tests.Scheduling.Services;

public sealed class NoOpSchedulingLoanOutServiceTests
{
    [Fact]
    public async Task FilterAssignments_PreservesAllUsersForEveryAssignment()
    {
        var service = new NoOpSchedulingLoanOutService();
        var ct = TestContext.Current.CancellationToken;
        var users = new[] { Guid.NewGuid(), Guid.NewGuid() };
        var start = new DateTimeOffset(2026, 10, 26, 9, 0, 0, TimeSpan.Zero);
        ShiftAssignmentLoanOutContext[] assignments = [new(start, start.AddHours(1)), new(start.AddDays(1), null)];

        var results = await service.GetAssignableUserIdsByOccurrenceAsync(users, assignments, 5, "UTC", ct);

        Assert.Equal(2, results.Count);
        Assert.All(results, result => Assert.Equal(users, result));
        Assert.Empty(await service.GetAssignableUserIdsByOccurrenceAsync(users, [], null, null, ct));
        Assert.All(
            await service.GetAssignableUserIdsByOccurrenceAsync([], assignments, 5, "UTC", ct),
            result => Assert.Empty(result)
        );
    }

    [Fact]
    public async Task DisabledLoanOut_DoesNotReportOverlapsOrTransferShifts()
    {
        var service = new NoOpSchedulingLoanOutService();
        var ct = TestContext.Current.CancellationToken;

        Assert.False(await service.HasShiftAtLoanOutLocationAsync([], 9, ct));
        Assert.False(await service.TransferLoanedOutUserShift(new LoanOutEntry(), true, ct));
        Assert.False(await service.TransferLoanedOutUserShift(new LoanOutSeries(), true, ct));
        await service.RestoreHomeLocationUserShiftsAsync([], ct);
    }

    [Fact]
    public async Task DisabledLoanOut_OperationsRespectCancellation()
    {
        var service = new NoOpSchedulingLoanOutService();
        using var cancellation = CancellationTokenSource.CreateLinkedTokenSource(TestContext.Current.CancellationToken);
        cancellation.Cancel();
        Func<Task>[] operations =
        [
            () => service.RestoreHomeLocationUserShiftsAsync([], cancellation.Token),
            () => service.GetAssignableUserIdsByOccurrenceAsync([], [], null, null, cancellation.Token),
            () => service.HasShiftAtLoanOutLocationAsync([], 9, cancellation.Token),
            () => service.TransferLoanedOutUserShift(new LoanOutEntry(), cancellationToken: cancellation.Token),
            () => service.TransferLoanedOutUserShift(new LoanOutSeries(), cancellationToken: cancellation.Token),
        ];
        foreach (var operation in operations)
            await Assert.ThrowsAnyAsync<OperationCanceledException>(operation);
    }
}
