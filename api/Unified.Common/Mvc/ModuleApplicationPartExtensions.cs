using System.Reflection;
using Microsoft.AspNetCore.Mvc.ApplicationParts;
using Microsoft.Extensions.DependencyInjection;

namespace Unified.Common.Mvc;

public static class ModuleApplicationPartExtensions
{
    public static IMvcBuilder AddConditionalApplicationPart<TMarker>(this IMvcBuilder mvcBuilder, bool isEnabled)
    {
        var moduleAssembly = typeof(TMarker).Assembly;

        mvcBuilder.ConfigureApplicationPartManager(manager =>
            ConfigureApplicationParts(manager, moduleAssembly, isEnabled)
        );

        return mvcBuilder;
    }

    private static void ConfigureApplicationParts(
        ApplicationPartManager manager,
        Assembly moduleAssembly,
        bool isEnabled
    )
    {
        if (isEnabled)
        {
            return;
        }

        var assemblyName = moduleAssembly.GetName().Name;
        var existingParts = manager.ApplicationParts.Where(part => part.Name == assemblyName).ToList();

        foreach (var part in existingParts)
        {
            manager.ApplicationParts.Remove(part);
        }
    }
}
