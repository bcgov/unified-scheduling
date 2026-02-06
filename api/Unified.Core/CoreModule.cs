using Microsoft.AspNetCore.Routing;
using Microsoft.AspNetCore.Builder;
using Microsoft.Extensions.DependencyInjection;

namespace Unified.Core;

public static class CoreModule
{
    public static IServiceCollection AddCoreModule(this IServiceCollection s) => s
        .AddValidation();
}