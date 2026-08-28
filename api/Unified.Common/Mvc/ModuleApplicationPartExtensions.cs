using Microsoft.Extensions.DependencyInjection;

namespace Unified.Common.Mvc;

public static class ModuleApplicationPartExtensions
{
    public static IMvcBuilder AddConditionalApplicationPart<TMarker>(this IMvcBuilder mvcBuilder, bool isEnabled)
    {
        if (isEnabled)
        {
            return mvcBuilder;
        }

        var moduleAssembly = typeof(TMarker).Assembly;
        var manager = mvcBuilder.PartManager;
        var assemblyName = moduleAssembly.GetName().Name;
        var existingParts = manager.ApplicationParts.Where(part => part.Name == assemblyName).ToList();

        foreach (var part in existingParts)
        {
            manager.ApplicationParts.Remove(part);
        }

        return mvcBuilder;
    }
}
