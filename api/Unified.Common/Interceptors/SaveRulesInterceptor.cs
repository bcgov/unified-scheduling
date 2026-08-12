using Microsoft.EntityFrameworkCore;
using Microsoft.EntityFrameworkCore.Diagnostics;
using Microsoft.Extensions.Logging;

namespace Unified.Common.Interceptors;

/// <summary>
/// EF Core interceptor that runs all registered ISaveRules before SaveChanges.
/// Auto-discovers rules via DI container (IEnumerable&lt;ISaveRule&gt;).
/// Runs all rules inside the transaction - any exception causes rollback.
/// </summary>
public sealed class SaveRulesInterceptor(IEnumerable<ISaveRule> rules, ILogger<SaveRulesInterceptor> logger)
    : SaveChangesInterceptor
{
    public override async ValueTask<InterceptionResult<int>> SavingChangesAsync(
        DbContextEventData eventData,
        InterceptionResult<int> result,
        CancellationToken cancellationToken = default
    )
    {
        if (eventData.Context is null)
            return result;

        // Run all rules before SaveChanges
        foreach (var rule in rules)
        {
            logger.LogDebug("Running rule: {RuleName}", rule.GetType().Name);
            try
            {
                await rule.ExecuteAsync(eventData.Context, cancellationToken);
            }
            catch (Exception ex)
            {
                logger.LogError(ex, "Rule {RuleName} failed: {Message}", rule.GetType().Name, ex.Message);
                throw; // Re-throw original exception to propagate error message and trigger rollback
            }
        }

        return result;
    }
}
