namespace Unified.Infrastructure.ErrorHandling;

/// <summary>
/// Thrown when an authenticated user attempts an action they are not permitted to perform.
/// Mapped to a 403 ProblemDetails response by <see cref="GlobalExceptionHandler"/>.
/// </summary>
public sealed class ForbiddenException : Exception
{
    public ForbiddenException(string message) : base(message) { }

    public ForbiddenException() : base("You do not have permission to perform this action.") { }
}
