using FluentValidation;
using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Mvc;
using Unified.TimeOff;
using Unified.TimeOff.Controllers;
using Unified.TimeOff.Models;
using Unified.TimeOff.Services;
using Unified.TimeOff.Validators;

namespace Unified.Tests.TimeOff.Controllers;

public sealed class LeaveTypeControllerTests
{
    private readonly FakeLeaveTypeService _service = new();
    private readonly LeaveTypeController _controller;

    public LeaveTypeControllerTests()
    {
        _controller = new LeaveTypeController(_service, new LeaveTypeRequestValidator());
    }

    [Fact]
    public async Task GetLeaveTypes_ReturnsOkWithServiceResult()
    {
        _service.All = [new LeaveTypeResponse { Id = 1, Name = "Vacation" }];

        var result = await _controller.GetLeaveTypes(TestContext.Current.CancellationToken);

        var ok = Assert.IsType<OkObjectResult>(result.Result);
        Assert.Same(_service.All, ok.Value);
    }

    [Fact]
    public async Task GetLeaveTypeById_WhenFound_ReturnsOk()
    {
        _service.Single = new LeaveTypeResponse { Id = 3, Name = "Sick" };

        var result = await _controller.GetLeaveTypeById(3, TestContext.Current.CancellationToken);

        Assert.Same(_service.Single, Assert.IsType<OkObjectResult>(result.Result).Value);
    }

    [Fact]
    public async Task GetLeaveTypeById_WhenMissing_ReturnsNotFound()
    {
        var result = await _controller.GetLeaveTypeById(3, TestContext.Current.CancellationToken);

        Assert.IsType<NotFoundResult>(result.Result);
    }

    [Fact]
    public async Task CreateLeaveType_WhenValid_ReturnsCreatedWithLocation()
    {
        _service.Single = new LeaveTypeResponse { Id = 7, Name = "Vacation" };

        var result = await _controller.CreateLeaveType(ValidRequest(), TestContext.Current.CancellationToken);

        var created = Assert.IsType<CreatedResult>(result.Result);
        Assert.Equal("/api/leave/leave-types/7", created.Location);
        Assert.Same(_service.Single, created.Value);
    }

    [Fact]
    public async Task CreateLeaveType_WhenInvalid_ThrowsValidationExceptionWithoutCallingService()
    {
        await Assert.ThrowsAsync<ValidationException>(() =>
            _controller.CreateLeaveType(ValidRequest() with { Name = "" }, TestContext.Current.CancellationToken)
        );

        Assert.Equal(0, _service.WriteCalls);
    }

    [Fact]
    public async Task UpdateLeaveType_WhenFound_ReturnsOk()
    {
        _service.Single = new LeaveTypeResponse { Id = 2, Name = "Vacation" };

        var result = await _controller.UpdateLeaveType(2, ValidRequest(), TestContext.Current.CancellationToken);

        Assert.Same(_service.Single, Assert.IsType<OkObjectResult>(result.Result).Value);
    }

    [Fact]
    public async Task UpdateLeaveType_WhenMissing_ReturnsNotFound()
    {
        var result = await _controller.UpdateLeaveType(2, ValidRequest(), TestContext.Current.CancellationToken);

        Assert.IsType<NotFoundResult>(result.Result);
    }

    [Fact]
    public async Task UpdateLeaveType_WhenInvalid_ThrowsValidationException()
    {
        await Assert.ThrowsAsync<ValidationException>(() =>
            _controller.UpdateLeaveType(
                2,
                ValidRequest() with
                {
                    Name = new string('a', 51),
                },
                TestContext.Current.CancellationToken
            )
        );

        Assert.Equal(0, _service.WriteCalls);
    }

    [Fact]
    public async Task DeleteLeaveType_WhenDeleted_ReturnsNoContent()
    {
        _service.DeleteResult = true;

        Assert.IsType<NoContentResult>(await _controller.DeleteLeaveType(1, TestContext.Current.CancellationToken));
    }

    [Fact]
    public async Task DeleteLeaveType_WhenMissing_ReturnsNotFound()
    {
        Assert.IsType<NotFoundResult>(await _controller.DeleteLeaveType(1, TestContext.Current.CancellationToken));
    }

    [Theory]
    [InlineData(nameof(LeaveTypeController.GetLeaveTypes), LeavePolicies.LeaveTypesView)]
    [InlineData(nameof(LeaveTypeController.GetLeaveTypeById), LeavePolicies.LeaveTypesView)]
    [InlineData(nameof(LeaveTypeController.CreateLeaveType), TimeOffPolicies.TimeOffCreateAndAssign)]
    [InlineData(nameof(LeaveTypeController.UpdateLeaveType), TimeOffPolicies.TimeOffEdit)]
    [InlineData(nameof(LeaveTypeController.DeleteLeaveType), TimeOffPolicies.TimeOffDelete)]
    public void Actions_RequireExpectedAuthorizationPolicy(string actionName, string expectedPolicy)
    {
        var method = typeof(LeaveTypeController).GetMethod(actionName)!;

        var policy = Assert.Single(method.GetCustomAttributes(typeof(AuthorizeAttribute), inherit: false));

        Assert.Equal(expectedPolicy, ((AuthorizeAttribute)policy).Policy);
    }

    [Fact]
    public void Controller_RequiresAuthenticationByDefault()
    {
        Assert.NotEmpty(typeof(LeaveTypeController).GetCustomAttributes(typeof(AuthorizeAttribute), inherit: false));
    }

    private static LeaveTypeRequest ValidRequest() =>
        new()
        {
            Name = "Vacation",
            Description = "Paid vacation",
            IsPaid = true,
        };

    private sealed class FakeLeaveTypeService : ILeaveTypeService
    {
        public IReadOnlyCollection<LeaveTypeResponse> All { get; set; } = [];

        public LeaveTypeResponse? Single { get; set; }

        public bool DeleteResult { get; set; }

        public int WriteCalls { get; private set; }

        public Task<IReadOnlyCollection<LeaveTypeResponse>> GetLeaveTypesAsync(CancellationToken cancellationToken) =>
            Task.FromResult(All);

        public Task<LeaveTypeResponse?> GetLeaveTypeByIdAsync(int id, CancellationToken cancellationToken) =>
            Task.FromResult(Single);

        public Task<LeaveTypeResponse> CreateLeaveTypeAsync(
            LeaveTypeRequest request,
            CancellationToken cancellationToken
        )
        {
            WriteCalls++;
            return Task.FromResult(Single!);
        }

        public Task<LeaveTypeResponse?> UpdateLeaveTypeAsync(
            int id,
            LeaveTypeRequest request,
            CancellationToken cancellationToken
        )
        {
            WriteCalls++;
            return Task.FromResult(Single);
        }

        public Task<bool> DeleteLeaveTypeAsync(int id, CancellationToken cancellationToken)
        {
            WriteCalls++;
            return Task.FromResult(DeleteResult);
        }
    }
}
