using Microsoft.Extensions.Configuration;
using Microsoft.Extensions.DependencyInjection;
using Microsoft.Extensions.Options;
using Unified.Authorization;
using Unified.Calendar;
using Unified.Calendar.Services;
using Unified.Common.FeatureFlags;
using Unified.Common.Options;
using Unified.Common.Seeding;
using Unified.Db;
using Unified.LoanOut.FeatureFlags;
using Unified.LoanOut.Mappers;
using Unified.LoanOut.Models;
using Unified.LoanOut.Seeders;
using Unified.LoanOut.Services;
using Unified.LoanOut.Validators;

namespace Unified.LoanOut;

public static class LoanOutModule
{
    public static bool IsModuleEnabled(IConfiguration config) =>
        config.GetSection(LoanOutFeatureFlags.Section).Get<LoanOutFeatureFlags>()?.Enabled ?? false;

    public static IServiceCollection AddLoanOutModule(this IServiceCollection services, IConfiguration config)
    {
        services
            .AddOptions<LoanOutFeatureFlags>()
            .BindConfiguration(LoanOutFeatureFlags.Section)
            .ValidateDataAnnotations()
            .ValidateOnStart();
        services.AddSingleton<
            IValidateOptions<LoanOutFeatureFlags>,
            RequiredBooleanOptionsValidator<LoanOutFeatureFlags>
        >();
        services.AddSingleton<IFeatureFlags>(sp => sp.GetRequiredService<IOptions<LoanOutFeatureFlags>>().Value);

        if (!IsModuleEnabled(config))
            return services;

        if (!CalendarModule.IsModuleEnabled(config))
            throw new InvalidOperationException("LoanOut requires the Calendar module to be enabled.");

        services.AddScoped<ILoanOutService, LoanOutService>();
        LoanOutServiceProvider.Register(services);
        services.AddScoped<LoanOutEventMapper>();
        services.AddScoped<LoanOutCalendarMapper>();
        services.AddScoped<LoanOutSeriesMaterializationHandler>();
        services.AddScoped<
            IEventSeriesMaterializationHandler<LoanOutSeriesMaterializationContext>,
            LoanOutSeriesMaterializationHandler
        >(sp => sp.GetRequiredService<LoanOutSeriesMaterializationHandler>());
        services.AddScoped<LoanOutRequestValidator>();
        services.AddScoped<LoanOutSeriesRequestValidator>();
        services.AddScoped<LoanOutUserSearchRequestValidator>();
        services.AddSeeder<UnifiedDbContext, LoanOutEventTypeSeeder>();
        services.AddScoped<ILoanOutCalendarDataProvider, LoanOutCalendarDataProvider>();

        services
            .AddAuthorizationBuilder()
            .AddPermissionPolicy(Permissions.LoanOutView)
            .AddPermissionPolicy(Permissions.LoanOutCreateAndAssign)
            .AddPermissionPolicy(Permissions.LoanOutEdit)
            .AddPermissionPolicy(Permissions.LoanOutDelete)
            .AddAnyPermissionPolicy(
                LoanOutPolicies.LoanOutViewOrUser,
                Permissions.LoanOutView,
                Permissions.UsersLoanOutView
            )
            .AddAnyPermissionPolicy(
                LoanOutPolicies.LoanOutCreateOrUser,
                Permissions.LoanOutCreateAndAssign,
                Permissions.UsersLoanOutCreate
            )
            .AddAnyPermissionPolicy(
                LoanOutPolicies.LoanOutEditOrUser,
                Permissions.LoanOutEdit,
                Permissions.UsersLoanOutEdit
            )
            .AddAnyPermissionPolicy(
                LoanOutPolicies.LoanOutDeleteOrUser,
                Permissions.LoanOutDelete,
                Permissions.UsersLoanOutDelete
            )
            .AddAnyPermissionPolicy(
                LoanOutPolicies.LoanOutTypesView,
                Permissions.LoanOutView,
                Permissions.UsersLoanOutView
            );

        return services;
    }
}
