using System.Linq;
using System.Reflection;
using Microsoft.EntityFrameworkCore;

namespace Unified.Db;

/// <summary>
/// ModelBuilderExtensions static class, provides extension methods for ModelBuilder objects.
/// </summary>
public static class ModelBuilderExtensions
{
    /// <summary>
    /// Applies all of the IEntityTypeConfiguration objects in all of the assemblies of the current domain.
    /// </summary>
    /// <param name="modelBuilder"></param>
    /// <returns></returns>
    public static ModelBuilder ApplyAllConfigurations(this ModelBuilder modelBuilder)
    {
        var assemblies = AppDomain.CurrentDomain.GetAssemblies();
        foreach (var assembly in assemblies)
        {
            modelBuilder.ApplyAllConfigurations(assembly, null);
        }

        return modelBuilder;
    }

    /// <summary>
    /// Applies all of the IEntityTypeConfiguration objects in the specified assembly.
    /// </summary>
    /// <param name="modelBuilder"></param>
    /// <param name="assembly"></param>
    /// <param name="context"></param>
    /// <returns></returns>
    public static ModelBuilder ApplyAllConfigurations(
        this ModelBuilder modelBuilder,
        Assembly assembly,
        DbContext? context = null
    )
    {
        ArgumentNullException.ThrowIfNull(assembly);

        var type = typeof(IEntityTypeConfiguration<>);
        var configurations = assembly
            .GetTypes()
            .Where(t =>
                t.IsClass
                && !t.ContainsGenericParameters
                && t.GetInterfaces()
                    .Any(i => i.IsGenericType && i.GetGenericTypeDefinition() == type)
            );

        var method = typeof(ModelBuilder)
            .GetMethods(BindingFlags.Instance | BindingFlags.Public)
            .First(m =>
                m.Name.Equals(nameof(ModelBuilder.ApplyConfiguration))
                && m.GetParameters()[0].ParameterType.GetGenericTypeDefinition() == type
            );

        foreach (var configurationType in configurations)
        {
            var includeContext =
                context != null
                && configurationType
                    .GetConstructors()
                    .Any(c =>
                        c.GetParameters()
                            .Any(p => p.ParameterType.IsAssignableFrom(context.GetType()))
                    );

            var entityConfig = includeContext
                ? Activator.CreateInstance(configurationType, context)
                : Activator.CreateInstance(configurationType);
            if (entityConfig == null)
                continue;

            var entityType = configurationType
                .GetInterfaces()
                .First(i => i.IsGenericType && i.GetGenericTypeDefinition() == type)
                .GetGenericArguments()[0];

            var applyConfigurationMethod = method.MakeGenericMethod(entityType);
            applyConfigurationMethod.Invoke(modelBuilder, new[] { entityConfig });
        }

        return modelBuilder;
    }
}
