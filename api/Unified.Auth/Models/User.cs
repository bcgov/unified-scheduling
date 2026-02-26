namespace Unified.Auth.Models;

public sealed record User(
    Guid Id,
    string IdirName,
    Guid? IdirId,
    Guid? KeyCloakId,
    bool IsEnabled,
    string FirstName,
    string LastName,
    string Email,
    int? HomeLocationId,
    DateTimeOffset? LastLogin
);
