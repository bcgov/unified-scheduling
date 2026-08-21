namespace Unified.Api.Models;

public sealed record TestEmailRequest
{
    public required string Recipient { get; init; }
}

public sealed record TestEmailResponse
{
    public required string TransactionId { get; init; }

    public required string Tag { get; init; }

    public IReadOnlyCollection<string> MessageIds { get; init; } = [];
}
