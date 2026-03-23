using System.ComponentModel.DataAnnotations;
using Unified.Db.Models.UserManagement;

namespace Unified.UserManagement.Models;

public sealed record CreateUserRequest(
    [Required, StringLength(200)] string IdirName,
    Guid? IdirId,
    bool IsEnabled,
    [Required, StringLength(150)] string FirstName,
    [Required, StringLength(150)] string LastName,
    [Required, EmailAddress, StringLength(320)] string Email,
    Gender Gender,
    [Required, StringLength(150)] string Rank,
    string BadgeNumber,
    int? HomeLocationId
);
