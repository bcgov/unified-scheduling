using System.ComponentModel.DataAnnotations;

namespace Unified.Auth.Models;

public sealed record CreateUserRequest(
    [Required, StringLength(200)] string IdirName,
    Guid? IdirId,
    bool IsEnabled,
    [Required, StringLength(150)] string FirstName,
    [Required, StringLength(150)] string LastName,
    [Required, EmailAddress, StringLength(320)] string Email,
    int? HomeLocationId
);
