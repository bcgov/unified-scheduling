using System.Security.Claims;
using Microsoft.AspNetCore.Http;
using Microsoft.AspNetCore.Mvc;
using Unified.Authorization.Claims;
using Unified.Stats.Controllers;
using Unified.Stats.Models;
using Unified.Stats.Services;
using Unified.Stats.Validators;

namespace Unified.Tests.Stats.Controllers;

public class StatRecordsControllerTests
{
    public static TheoryData<ActionName, string?> InvalidCallerCases =>
        new()
        {
            { ActionName.GetAll, null },
            { ActionName.GetAll, "invalid" },
            { ActionName.Create, null },
            { ActionName.Create, "invalid" },
            { ActionName.CreateBatch, null },
            { ActionName.CreateBatch, "invalid" },
            { ActionName.Update, null },
            { ActionName.Update, "invalid" },
            { ActionName.SaveDay, null },
            { ActionName.SaveDay, "invalid" },
            { ActionName.Delete, null },
            { ActionName.Delete, "invalid" },
        };

    [Theory]
    [MemberData(nameof(InvalidCallerCases))]
    public async Task Action_InvalidApplicationUserId_ReturnsForbidWithoutCallingService(
        ActionName action,
        string? userIdClaim
    )
    {
        var service = new RecordingStatRecordService();
        var controller = CreateController(service, userIdClaim);

        var result = await InvokeAction(controller, action);

        Assert.IsType<ForbidResult>(result);
        Assert.Equal(0, service.CallCount);
    }

    [Fact]
    public async Task GetAll_ValidApplicationUserId_InvokesServiceAndReturnsOk()
    {
        var service = new RecordingStatRecordService();
        var controller = CreateController(service, Guid.NewGuid().ToString());

        var result = await controller.GetAll(null, TestContext.Current.CancellationToken);

        Assert.IsType<OkObjectResult>(result.Result);
        Assert.Equal(1, service.CallCount);
    }

    private static StatRecordsController CreateController(RecordingStatRecordService service, string? userIdClaim)
    {
        var claims = userIdClaim is null ? [] : new[] { new Claim(UnifiedClaimTypes.UserId, userIdClaim) };
        return new StatRecordsController(service, new StatRecordRequestValidator())
        {
            ControllerContext = new ControllerContext
            {
                HttpContext = new DefaultHttpContext
                {
                    User = new ClaimsPrincipal(new ClaimsIdentity(claims, "Test")),
                },
            },
        };
    }

    private static async Task<IActionResult> InvokeAction(StatRecordsController controller, ActionName action)
    {
        var cancellationToken = TestContext.Current.CancellationToken;
        return action switch
        {
            ActionName.GetAll => (await controller.GetAll(null, cancellationToken)).Result!,
            ActionName.Create => (await controller.Create(CreateRequest(), cancellationToken)).Result!,
            ActionName.CreateBatch => (await controller.CreateBatch([CreateRequest()], cancellationToken)).Result!,
            ActionName.Update => (await controller.Update(1, CreateRequest(), cancellationToken)).Result!,
            ActionName.SaveDay => (await controller.SaveDay(CreateSaveDayRequest(), cancellationToken)).Result!,
            ActionName.Delete => await controller.Delete(1, cancellationToken),
            _ => throw new ArgumentOutOfRangeException(nameof(action)),
        };
    }

    private static StatRecordRequest CreateRequest() =>
        new()
        {
            DateFrom = new DateOnly(2026, 1, 1),
            DateTo = new DateOnly(2026, 1, 1),
            PeriodType = "Day",
            UserId = Guid.NewGuid(),
            LocationId = 1,
            SubCategoryMetricId = 1,
            Value = 1,
        };

    private static SaveDayRequest CreateSaveDayRequest() =>
        new()
        {
            Date = new DateOnly(2026, 1, 1),
            LocationId = 1,
            Status = "Draft",
            GroupId = 1,
            Records = [],
        };

    public enum ActionName
    {
        GetAll,
        Create,
        CreateBatch,
        Update,
        SaveDay,
        Delete,
    }

    private sealed class RecordingStatRecordService : IStatRecordService
    {
        public int CallCount { get; private set; }

        public Task<IReadOnlyCollection<StatRecordResponse>> GetAllAsync(StatRecordQueryParams? queryParams = null, CancellationToken cancellationToken = default)
        {
            CallCount++;
            return Task.FromResult<IReadOnlyCollection<StatRecordResponse>>([]);
        }

        public Task<StatRecordResponse?> GetByIdAsync(int id, CancellationToken cancellationToken = default)
        {
            CallCount++;
            return Task.FromResult<StatRecordResponse?>(null);
        }

        public Task<StatRecordResponse> CreateAsync(StatRecordRequest request, Guid callerUserId, bool callerCanEnterForOthers, CancellationToken cancellationToken = default)
        {
            CallCount++;
            return Task.FromResult(new StatRecordResponse());
        }

        public Task<IReadOnlyCollection<StatRecordResponse>> CreateBatchAsync(IReadOnlyList<StatRecordRequest> requests, Guid callerUserId, bool callerCanEnterForOthers, CancellationToken cancellationToken = default)
        {
            CallCount++;
            return Task.FromResult<IReadOnlyCollection<StatRecordResponse>>([]);
        }

        public Task<StatRecordResponse?> UpdateAsync(int id, StatRecordRequest request, Guid callerUserId, bool callerCanEnterForOthers, CancellationToken cancellationToken = default)
        {
            CallCount++;
            return Task.FromResult<StatRecordResponse?>(new StatRecordResponse());
        }

        public Task<bool> DeleteAsync(int id, Guid callerUserId, bool callerCanEnterForOthers, CancellationToken cancellationToken = default)
        {
            CallCount++;
            return Task.FromResult(true);
        }

        public Task<IReadOnlyCollection<StatRecordResponse>> SaveDayAsync(SaveDayRequest request, Guid callerUserId, bool callerCanEnterForOthers, bool canOverrideSignedOff = false, CancellationToken cancellationToken = default)
        {
            CallCount++;
            return Task.FromResult<IReadOnlyCollection<StatRecordResponse>>([]);
        }

        public Task<bool> IsLocationLevelGroupAsync(int groupId, CancellationToken cancellationToken = default)
        {
            CallCount++;
            return Task.FromResult(false);
        }
    }
}
