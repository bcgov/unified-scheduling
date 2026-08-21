namespace Unified.Infrastructure.Email.Ches;

internal sealed class ChesAuthenticationException : Exception
{
    public ChesAuthenticationException(int statusCode)
        : base($"CHES authentication failed with HTTP status {statusCode}.")
    {
        StatusCode = statusCode;
    }

    public ChesAuthenticationException(string message)
        : base(message) { }

    public ChesAuthenticationException(string message, Exception innerException)
        : base(message, innerException) { }

    public int? StatusCode { get; }
}
