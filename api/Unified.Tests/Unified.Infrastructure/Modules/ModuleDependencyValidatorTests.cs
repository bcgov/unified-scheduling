using Unified.Calendar;
using Unified.FeatureFlags;
using Unified.Infrastructure.Modules;
using Unified.Scheduling;
using Unified.UserManagement;

namespace Unified.Tests.Infrastructure.Modules;

public class ModuleDependencyValidatorTests
{
    [Fact]
    public void Validate_WhenSchedulingEnabledAndCalendarDisabled_ThrowsInvalidOperationException()
    {
        // Arrange
        var featureFlags = new FeatureFlags.FeatureFlags { CalendarModule = false, SchedulingModule = true };

        // Act
        var exception = Assert.Throws<InvalidOperationException>(() =>
            ModuleDependencyValidator.Validate(CreateKnownModules(), featureFlags)
        );

        // Assert
        Assert.Contains(CalendarModule.ModuleName, exception.Message);
        Assert.Contains(SchedulingModule.ModuleName, exception.Message);
    }

    [Fact]
    public void Validate_WhenSchedulingAndCalendarEnabledAndUserManagementRegistered_DoesNotThrow()
    {
        // Arrange
        var featureFlags = new FeatureFlags.FeatureFlags { CalendarModule = true, SchedulingModule = true };

        // Act
        var exception = Record.Exception(() => ModuleDependencyValidator.Validate(CreateKnownModules(), featureFlags));

        // Assert
        Assert.Null(exception);
    }

    [Theory]
    [InlineData(false)]
    [InlineData(true)]
    public void Validate_WhenSchedulingDisabled_DoesNotRequireCalendar(bool calendarModuleEnabled)
    {
        // Arrange
        var featureFlags = new FeatureFlags.FeatureFlags
        {
            CalendarModule = calendarModuleEnabled,
            SchedulingModule = false,
        };

        // Act
        var exception = Record.Exception(() => ModuleDependencyValidator.Validate(CreateKnownModules(), featureFlags));

        // Assert
        Assert.Null(exception);
    }

    [Fact]
    public void SchedulingModuleDescriptor_WhenCreated_DeclaresCalendarAndUserManagementDependencies()
    {
        // Act
        var module = SchedulingModule.Descriptor;

        // Assert
        Assert.Equal(SchedulingModule.ModuleName, module.Name);
        Assert.Equal(
            [CalendarModule.ModuleName, UserManagementModule.ModuleName],
            module.RequiredModuleNames
        );
    }

    [Fact]
    public void UserManagementModuleDescriptor_WhenCreated_IsAlwaysEnabledWithoutDependencies()
    {
        // Act
        var module = UserManagementModule.Descriptor;

        // Assert
        Assert.Equal(UserManagementModule.ModuleName, module.Name);
        Assert.True(module.IsEnabled(new FeatureFlags.FeatureFlags()));
        Assert.Empty(module.RequiredModuleNames);
    }

    [Fact]
    public void Validate_WhenRequiredModuleIsNotRegistered_ThrowsInvalidOperationException()
    {
        // Arrange
        var featureFlags = new FeatureFlags.FeatureFlags { CalendarModule = true, SchedulingModule = true };
        UnifiedModuleDescriptor[] modules =
        [
            new("DependentModule", IsEnabled: _ => true, RequiredModuleNames: ["MissingModule"]),
        ];

        // Act
        var exception = Assert.Throws<InvalidOperationException>(() =>
            ModuleDependencyValidator.Validate(modules, featureFlags)
        );

        // Assert
        Assert.Contains("DependentModule", exception.Message);
        Assert.Contains("MissingModule", exception.Message);
    }

    [Fact]
    public void Validate_WhenDuplicateModuleNamesExist_ThrowsInvalidOperationException()
    {
        // Arrange
        var featureFlags = new FeatureFlags.FeatureFlags();
        UnifiedModuleDescriptor[] modules =
        [
            new("DuplicateModule", IsEnabled: _ => false, RequiredModuleNames: []),
            new("DuplicateModule", IsEnabled: _ => false, RequiredModuleNames: []),
        ];

        // Act
        var exception = Assert.Throws<InvalidOperationException>(() =>
            ModuleDependencyValidator.Validate(modules, featureFlags)
        );

        // Assert
        Assert.Contains("duplicate module name", exception.Message);
        Assert.Contains("DuplicateModule", exception.Message);
    }

    private static IReadOnlyCollection<UnifiedModuleDescriptor> CreateKnownModules() =>
        [CalendarModule.Descriptor, SchedulingModule.Descriptor, UserManagementModule.Descriptor];
}
