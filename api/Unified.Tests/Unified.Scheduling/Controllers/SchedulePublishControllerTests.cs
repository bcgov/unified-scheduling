using System.Security.Claims;
using Microsoft.AspNetCore.Http;
using Microsoft.AspNetCore.Mvc;
using Unified.Authorization;
using Unified.Authorization.Claims;
using Unified.Scheduling.Controllers;
using Unified.Scheduling.Models;
using Unified.Scheduling.Services;
using Unified.Scheduling.Validators;

namespace Unified.Tests.Scheduling.Controllers;

public sealed class SchedulePublishControllerTests
{
    [Theory]
    [InlineData(false, false)]
    [InlineData(false, true)]
    [InlineData(true, false)]
    [InlineData(true, true)]
    public async Task PublicationResponse_TrainingConflict_RedactsWithoutTrainingView(
        bool canViewTraining,
        bool publish
    )
    {
        var start = new DateTimeOffset(2026, 10, 6, 16, 0, 0, TimeSpan.Zero);
        var trainingConflict = new SchedulePublishConflictResponse(
            "conflict:training.user-training.7",
            "training",
            "training.user-training.7",
            "scheduling",
            "42",
            Guid.NewGuid(),
            start,
            start.AddHours(1)
        );
        var otherConflict = new SchedulePublishConflictResponse(
            "conflict:scheduling:42:43",
            "scheduling",
            "42",
            "scheduling",
            "43",
            Guid.NewGuid(),
            start,
            start.AddHours(1)
        );
        var preview = new SchedulePublishPreviewResponse(
            new SchedulePublishScopeResponse(
                5,
                new DateOnly(2026, 10, 6),
                new DateOnly(2026, 10, 6),
                "UTC",
                start,
                start.AddDays(1)
            ),
            new SchedulePublishCandidateCountsResponse(1, 1),
            new SchedulePublishWarningsResponse([], [], []),
            new SchedulePublishBlockersResponse([trainingConflict, otherConflict], []),
            false
        );
        var controller = new SchedulePublishController(
            new BlockedPublishService(preview),
            new SchedulePublishRequestValidator()
        )
        {
            ControllerContext = new ControllerContext
            {
                HttpContext = new DefaultHttpContext
                {
                    User = new ClaimsPrincipal(
                        new ClaimsIdentity(
                            canViewTraining
                                ? [new Claim(UnifiedClaimTypes.Permission, nameof(Permissions.UserTrainingsView))]
                                : []
                        )
                    ),
                },
            },
        };
        var request = new SchedulePublishRequest(5, new DateOnly(2026, 10, 6), new DateOnly(2026, 10, 6));

        var result = publish ? await controller.Publish(request, TestContext.Current.CancellationToken) : null;
        var response = publish
            ? Assert.IsType<SchedulePublishPreviewResponse>(Assert.IsType<ConflictObjectResult>(result!.Result).Value)
            : Assert.IsType<SchedulePublishPreviewResponse>(
                Assert
                    .IsType<OkObjectResult>(
                        (await controller.Preview(request, TestContext.Current.CancellationToken)).Result
                    )
                    .Value
            );

        Assert.False(response.CanPublish);
        Assert.Contains(otherConflict, response.Blockers.Conflicts);
        var visibleTrainingConflict = Assert.Single(
            response.Blockers.Conflicts,
            conflict => conflict.Id != otherConflict.Id
        );
        if (canViewTraining)
            Assert.Equal(trainingConflict, visibleTrainingConflict);
        else
        {
            Assert.Equal("restricted", visibleTrainingConflict.FirstSourceModule);
            Assert.Equal(string.Empty, visibleTrainingConflict.FirstEventId);
            Assert.DoesNotContain("training.user-training.7", System.Text.Json.JsonSerializer.Serialize(response));
        }
    }

    private sealed class BlockedPublishService(SchedulePublishPreviewResponse preview) : ISchedulePublishService
    {
        public Task<SchedulePublishPreviewResponse> PreviewAsync(
            SchedulePublishRequest request,
            CancellationToken cancellationToken = default
        ) => Task.FromResult(preview);

        public Task<SchedulePublishResult> PublishAsync(
            SchedulePublishRequest request,
            Guid? userId,
            CancellationToken cancellationToken = default
        ) => throw new SchedulePublishBlockedException(preview);
    }
}
