using Microsoft.AspNetCore.Mvc;
using Unified.Core.Controllers;
using Unified.Core.Models;
using Unified.Core.Services;

namespace Unified.Tests.Core.Controllers;

public class LookupControllerTests
{
    [Fact]
    public async Task GetAll_Should_Return_Ok_When_CodeType_Is_Supported()
    {
        // Arrange
        var expected = new List<LookupCodeResponse>
        {
            new()
            {
                Code = "SERGEANT",
                Description = "Sergeant",
                EffectiveDate = new DateTimeOffset(2020, 1, 1, 0, 0, 0, TimeSpan.Zero),
            },
        };

        var service = new FakeLookupService { Result = expected };
        var controller = new LookupController(service);

        // Act
        var result = await controller.GetAll(LookupCodeTypes.PositionTypes, TestContext.Current.CancellationToken);

        // Assert
        var okResult = Assert.IsType<OkObjectResult>(result.Result);
        var payload = Assert.IsAssignableFrom<IEnumerable<LookupCodeResponse>>(okResult.Value);
        var item = Assert.Single(payload);

        Assert.Equal("SERGEANT", item.Code);
        Assert.Equal(LookupCodeTypes.PositionTypes, service.LastCodeType);
    }

    [Fact]
    public async Task GetAll_Should_Return_NotFound_When_CodeType_Is_Unsupported()
    {
        // Arrange
        var service = new FakeLookupService { ThrowNotFound = true };
        var controller = new LookupController(service);

        // Act
        var result = await controller.GetAll(LookupCodeTypes.PositionTypes, TestContext.Current.CancellationToken);

        // Assert
        Assert.IsType<NotFoundResult>(result.Result);
    }

    private sealed class FakeLookupService : ILookupService
    {
        public IReadOnlyCollection<LookupCodeResponse> Result { get; init; } = [];

        public bool ThrowNotFound { get; init; }

        public LookupCodeTypes? LastCodeType { get; private set; }

        public Task<IReadOnlyCollection<LookupCodeResponse>> GetAllAsync(
            LookupCodeTypes codeType,
            CancellationToken cancellationToken = default
        )
        {
            LastCodeType = codeType;

            if (ThrowNotFound)
            {
                throw new KeyNotFoundException("Unsupported code type");
            }

            return Task.FromResult(Result);
        }
    }
}
