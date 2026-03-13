using System.ComponentModel.DataAnnotations;

namespace Unified.UserManagement.Models;

public sealed record UpdateUserRequest(
    bool IsEnabled,
    [Required, StringLength(150)] string FirstName,
    [Required, StringLength(150)] string LastName,
    [Required, EmailAddress, StringLength(320)] string Email,
    int? HomeLocationId
);
