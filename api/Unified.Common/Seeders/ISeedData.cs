namespace Unified.Common.Seeding;

public interface ISeedData<out TDefinition>
{
    IReadOnlyList<TDefinition> Definitions { get; }
}
