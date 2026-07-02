namespace Unified.Infrastructure.Modules;

public static class ModuleDependencyValidator
{
    public static void Validate(
        IReadOnlyCollection<UnifiedModuleDescriptor> modules,
        FeatureFlags.FeatureFlags featureFlags
    )
    {
        var duplicateModuleName = modules
            .GroupBy(module => module.Name)
            .Where(group => group.Count() > 1)
            .Select(group => group.Key)
            .FirstOrDefault();
        if (duplicateModuleName is not null)
        {
            throw new InvalidOperationException(
                $"Module registry contains duplicate module name {duplicateModuleName}."
            );
        }

        var modulesByName = modules.ToDictionary(module => module.Name);

        foreach (var module in modules.Where(module => module.IsEnabled(featureFlags)))
        {
            var missingDependencies = module
                .RequiredModuleNames.Where(requiredModuleName =>
                    !modulesByName.TryGetValue(requiredModuleName, out var requiredModule)
                    || !requiredModule.IsEnabled(featureFlags)
                )
                .ToList();

            if (missingDependencies.Count == 0)
                continue;

            throw new InvalidOperationException(
                $"{module.Name} requires {string.Join(", ", missingDependencies)}. "
                    + $"Enable {string.Join(", ", missingDependencies)} or disable {module.Name}."
            );
        }
    }
}
