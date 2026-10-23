using System.ComponentModel.DataAnnotations;
using Unified.Db.Models.Abstract;
using Unified.Db.Models.UserManagement;

namespace Unified.Db.Models.LoanOut;

public class LoanOutSeriesUser : BaseEntity
{
    [Key]
    public int Id { get; set; }

    public int LoanOutSeriesId { get; set; }

    public LoanOutSeries? LoanOutSeries { get; set; }

    public Guid UserId { get; set; }

    public User? User { get; set; }
}
