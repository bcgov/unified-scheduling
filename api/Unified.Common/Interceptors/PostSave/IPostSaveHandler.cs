using Microsoft.EntityFrameworkCore;

namespace Unified.Common.PostSave;

/// <summary>
/// Stages module-owned changes after the initial entity save has succeeded.
/// Register implementations as scoped services in the module that owns the behavior.
/// </summary>
/// <remarks>
/// Post-save does not mean post-commit: handlers execute sequentially inside the save's
/// transaction and can query the saved entity through the same scoped database context.
/// Do not save, commit, or perform external side effects. The interceptor saves handler changes
/// and the DbContext commits only after all handlers succeed. Exceptions roll back the entire save.
/// Unlike ISaveRule, this contract handles successful saves rather than validating pending changes.
/// Register directly with AddScoped&lt;IPostSaveHandler, THandler&gt;, like ISaveRule.
/// Do not inject DbContext in the constructor: use the context passed to HandleAsync to avoid
/// a circular dependency while EF constructs its interceptors.
/// Ordinary SaveChangesAsync calls invoke matching handlers.
/// Follow-up handler saves do not cascade into additional post-save handlers.
/// </remarks>
public interface IPostSaveHandler
{
    Type EntityType { get; }

    SaveAction Action { get; }

    Task HandleAsync(DbContext db, SaveContext context, CancellationToken cancellationToken);
}