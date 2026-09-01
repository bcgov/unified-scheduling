using Microsoft.Extensions.DependencyInjection;

namespace Unified.Common.Mvc;

public static class ModuleApplicationPartExtensions
{
    public static IMvcBuilder AddConditionalApplicationPart<TMarker>(this IMvcBuilder mvcBuilder, bool isEnabled)
    {
        var moduleAssembly = typeof(TMarker).Assembly;
        var manager = mvcBuilder.PartManager;
        var assemblyName = moduleAssembly.GetName().Name;

        // Ensure the assembly is either present exactly once (enabled) or absent (disabled).
        var existingParts = manager.ApplicationParts.Where(part => part.Name == assemblyName).ToList();
        foreach (var part in existingParts)
        {
            manager.ApplicationParts.Remove(part);
        }

        if (isEnabled)
        {
            manager.ApplicationParts.Add(
                new Microsoft.AspNetCore.Mvc.ApplicationParts.AssemblyPart(moduleAssembly)
            );
        }

        return mvcBuilder;
    }
}
