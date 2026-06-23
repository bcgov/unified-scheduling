using Microsoft.Extensions.Logging;
using Unified.Core.Models;
using Unified.Core.Services.Lookup;

namespace Unified.Core.Services;

public sealed class LookupService(IEnumerable<ILookupStrategy> lookupStrategies, ILogger<LookupService> logger)
    : ILookupService
{
    private readonly Dictionary<LookupCodeTypes, ILookupStrategy> _strategies = lookupStrategies.ToDictionary(
        strategy => strategy.CodeType
    );

    /// <inheritdoc />
    public Task<IReadOnlyCollection<LookupCodeResponse>> GetAllAsync(
        LookupCodeTypes codeType,
        CancellationToken cancellationToken = default
    )
    {
        logger.LogDebug("Retrieving lookup codes for code type {CodeType}", codeType);

        if (!_strategies.TryGetValue(codeType, out var strategy))
        {
            throw new KeyNotFoundException($"Lookup code type '{codeType}' is not supported.");
        }

        return strategy.GetAllAsync(cancellationToken);
    }
}
