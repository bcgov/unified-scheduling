using Microsoft.Extensions.DependencyInjection;
using Unified.Authorization;
using Unified.Reporting.Services.Reporting;

namespace Unified.Reporting;

public static class ReportingModule
{
    public static IServiceCollection AddReportingModule(this IServiceCollection services)
    {
        services.AddAuthorizationBuilder().AddPermissionPolicy(Permissions.ReportsGenerate);

        services.AddScoped<IReportQueryService, ReportQueryService>();
        return services;
    }
}
