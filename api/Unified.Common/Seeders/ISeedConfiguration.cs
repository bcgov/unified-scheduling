namespace Unified.Common.Seeding;

public interface ISeedConfiguration
{
    string Source { get; }
}

public interface ISeedConfiguration<out TDefinition> : ISeedConfiguration, ISeedData<TDefinition>
{
}