using Unified.Core.Models;
using Unified.Core.Services;
using Unified.Core.Services.Lookup;

namespace Unified.Tests.Core.Services;

public class LookupServiceTests
{
    [Fact]
    public async Task GetAllAsync_Should_Return_When_CodeType_Matches()
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

        var service = new LookupService([new FakeLookupStrategy(LookupCodeTypes.PositionTypes, expected)]);

        // Act
        var result = await service.GetAllAsync(LookupCodeTypes.PositionTypes, TestContext.Current.CancellationToken);

        // Assert
        var item = Assert.Single(result);
        Assert.Equal("SERGEANT", item.Code);
    }

    [Fact]
    public async Task GetAllAsync_Should_Throw_When_CodeType_NotFound()
    {
        // Arrange
        var service = new LookupService([new FakeLookupStrategy(LookupCodeTypes.PositionTypes, [])]);

        // Act + Assert
        // Note: LookupCodeTypes.PositionTypes=1, so trying to use (LookupCodeTypes)99 would fail if it existed
        // Since we only have PositionTypes, trying to get a code type that doesn't exist should throw
        await Assert.ThrowsAsync<KeyNotFoundException>(
            () => service.GetAllAsync((LookupCodeTypes)999, TestContext.Current.CancellationToken)
        );
    }

    private sealed class FakeLookupStrategy(LookupCodeTypes codeType, IReadOnlyCollection<LookupCodeResponse> result)
        : ILookupStrategy
    {
        public LookupCodeTypes CodeType { get; } = codeType;

        public Task<IReadOnlyCollection<LookupCodeResponse>> GetAllAsync(CancellationToken cancellationToken = default)
        {
            return Task.FromResult(result);
        }
    }
}
