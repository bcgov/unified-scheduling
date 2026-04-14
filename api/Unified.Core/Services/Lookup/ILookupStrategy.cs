using Unified.Core.Models;

namespace Unified.Core.Services.Lookup;

/// <summary>
/// Contract for a concrete lookup strategy.
/// </summary>
public interface ILookupStrategy
{
    /// <summary>
    /// Code type handled by this strategy, used by <see cref="Services.ILookupService"/> dispatch.
    /// </summary>
    LookupCodeTypes CodeType { get; }

    /// <summary>
    /// Returns all lookup values for this strategy.
    /// </summary>
    /// <param name="cancellationToken">Cancellation token.</param>
    /// <returns>A read-only collection of lookup code values.</returns>
    Task<IReadOnlyCollection<LookupCodeResponse>> GetAllAsync(CancellationToken cancellationToken = default);
}
