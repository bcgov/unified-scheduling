using Unified.Core.Models;

namespace Unified.Core.Services;

/// <summary>
/// Resolves and executes lookup strategies by code type.
/// </summary>
public interface ILookupService
{
    /// <summary>
    /// Gets all lookup values for the specified code type.
    /// </summary>
    /// <param name="codeType">The code type that identifies the lookup implementation.</param>
    /// <param name="cancellationToken">Cancellation token.</param>
    /// <returns>A read-only collection of lookup code values.</returns>
    /// <exception cref="KeyNotFoundException">Thrown when the code type is not registered.</exception>
    Task<IReadOnlyCollection<LookupCodeResponse>> GetAllAsync(
        LookupCodeTypes codeType,
        CancellationToken cancellationToken = default
    );
}
