using Microsoft.EntityFrameworkCore;

namespace Unified.Common.Interceptors.PostSave;

/// <summary>
/// Runs module-owned logic after a successful save and stages follow-up changes.
/// </summary>
/// <remarks>
/// Use for module-agnostic save concerns only.
/// Feature-specific business fan-out should use explicit in-process signals via IEventDispatcher.
/// Register handlers as scoped services.
/// Use the DbContext passed to HandleAsync (do not inject DbContext in the constructor).
/// Handlers stage changes only; no SaveChanges/commit/external side effects.
/// Handler changes are saved once by the interceptor and are not re-dispatched.
/// </remarks>
public interface IPostSaveHandler
{
    Type EntityType { get; }

    SaveAction Action { get; }

    Task HandleAsync(DbContext db, SaveContext context, CancellationToken cancellationToken);
}
