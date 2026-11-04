using System.ComponentModel.DataAnnotations;
using Unified.Db.Models.Abstract;
using Unified.Db.Models.UserManagement;

namespace Unified.Db.Models.LoanOut;

public class LoanOutEntryUser : BaseEntity
{
    [Key]
    public int Id { get; set; }

    public int LoanOutEntryId { get; set; }

    public LoanOutEntry? LoanOutEntry { get; set; }

    public Guid UserId { get; set; }

    public User? User { get; set; }
}
