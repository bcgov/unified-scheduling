using Microsoft.AspNetCore.Mvc.Filters;
using Microsoft.Extensions.DependencyInjection;
using Microsoft.Extensions.Options;
using Unified.Common.Filters;

namespace Unified.Tests.Common.Filters;

public class RequestFormLimitsFromOptionsAttributeTests
{
    public class TestOptions
    {
        public long UploadPhotoSizeLimitKb { get; set; } = 400;
    }

    [Fact]
    public void CreateInstance_WithValidProperty_ReturnsRequestFormLimitsFilter()
    {
        // Arrange
        var provider = BuildServiceProvider(new TestOptions { UploadPhotoSizeLimitKb = 400 });
        var attribute = new RequestFormLimitsFromOptionsAttribute<TestOptions>(
            nameof(TestOptions.UploadPhotoSizeLimitKb),
            multiplier: 1024
        );

        // Act
        var filter = attribute.CreateInstance(provider);

        // Assert — RequestFormLimitsAttribute's own CreateInstance produces a
        // RequestFormLimitsFilter, confirming our attribute unwraps the nested factory
        // instead of returning the inert IFilterFactory itself.
        Assert.Equal("RequestFormLimitsFilter", filter.GetType().Name);
    }

    [Fact]
    public void CreateInstance_WithMissingProperty_ThrowsInvalidOperationException()
    {
        // Arrange
        var provider = BuildServiceProvider(new TestOptions());
        var attribute = new RequestFormLimitsFromOptionsAttribute<TestOptions>("DoesNotExist");

        // Act
        var ex = Assert.Throws<InvalidOperationException>(() => attribute.CreateInstance(provider));

        // Assert
        Assert.Contains("DoesNotExist", ex.Message);
        Assert.Contains(nameof(TestOptions), ex.Message);
    }

    [Fact]
    public void IsReusable_IsFalse()
    {
        // Arrange
        var attribute = new RequestFormLimitsFromOptionsAttribute<TestOptions>(
            nameof(TestOptions.UploadPhotoSizeLimitKb)
        );

        // Act & Assert
        Assert.False(attribute.IsReusable);
    }

    private static IServiceProvider BuildServiceProvider(TestOptions options)
    {
        var services = new ServiceCollection();
        services.AddLogging();
        services.AddControllers();
        services.AddSingleton<IOptions<TestOptions>>(Options.Create(options));
        return services.BuildServiceProvider();
    }
}
