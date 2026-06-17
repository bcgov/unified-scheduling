namespace Unified.UserManagement.Models;

public class DeletedRoleDto
{
    public int Id { get; set; }

    public Guid DeletedBy { get; set; }

    public DateTimeOffset DeletedOn { get; set; }
}
