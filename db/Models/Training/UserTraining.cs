using System.ComponentModel.DataAnnotations;
using Unified.Db.Models.Abstract;
using Unified.Db.Models.UserManagement;

namespace Unified.Db.Models.Training;

public class UserTraining : BaseEntity
{
    [Key]
    public int Id { get; set; }

    [Required]
    public Guid UserId { get; set; }

    [Required]
    public int TrainingId { get; set; }

    [Required]
    public DateTimeOffset AwardedOn { get; set; }
    [Required]
    public DateTimeOffset EndingOn { get; set; }

    // The date the training expires. If null, the training does not expire.
    // Can be auto calculated based on the training's ValidityDays, but can also be manually set.
    public DateTimeOffset? ExpiryDate { get; set; }

    [Required]
    public string NoticeState { get; set; } = UserTrainingNoticeStates.None;

    public string? Notes { get; set; }

    public virtual User User { get; set; } = null!;

    public virtual Training Training { get; set; } = null!;
}

public static class UserTrainingNoticeStates
{
    public const string None = "None";
    public const string Pending = "Pending";
    public const string Sent = "Sent";
}
