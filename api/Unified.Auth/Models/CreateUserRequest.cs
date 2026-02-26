using System.ComponentModel.DataAnnotations;

namespace Unified.Auth.Models;

public sealed record CreateUserRequest(
    [property: Required, StringLength(200)] string IdirName,
    Guid? IdirId,
    Guid? KeyCloakId,
    bool IsEnabled,
    [property: Required, StringLength(150)] string FirstName,
    [property: Required, StringLength(150)] string LastName,
    [property: Required, EmailAddress, StringLength(320)] string Email,
    int? HomeLocationId,
    DateTimeOffset? LastLogin
);
