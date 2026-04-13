using System;
using System.ComponentModel.DataAnnotations;
using System.ComponentModel.DataAnnotations.Schema;
using Mapster;
using Unified.Db.Models.UserManagement;

/// <summary>
/// Credit to PIMS for this.
/// </summary>
namespace Unified.Db.Models.Abstract;

public abstract class BaseEntity
{
    #region Properties

    /// <summary>
    /// get/set - The foreign key to the user who created this entity.
    /// </summary>
    /// <value></value>
    [AdaptIgnore]
    public Guid? CreatedById { get; set; }

    /// <summary>
    /// get/set - The user who created this entity.
    /// </summary>
    /// <value></value>
    [AdaptIgnore]
    public User? CreatedBy { get; set; }

    /// <summary>
    /// get/set - When this entity was created.
    /// </summary>
    /// <value></value>
    [AdaptIgnore]
    public DateTimeOffset CreatedOn { get; set; }

    /// <summary>
    /// get/set - Who updated this entity last.
    /// </summary>
    /// <value></value>
    [AdaptIgnore]
    public Guid? UpdatedById { get; set; }

    /// <summary>
    /// get/set - The user updated this entity last.
    /// </summary>
    /// <value></value>
    [AdaptIgnore]
    public User? UpdatedBy { get; set; }

    /// <summary>
    /// get/set - When this entity was updated.
    /// </summary>
    /// <value></value>
    [AdaptIgnore]
    public DateTimeOffset? UpdatedOn { get; set; }

    /// <summary>
    /// get/set - The concurrency row version.
    /// </summary>
    /// <value></value>
    [Timestamp]
    [DatabaseGenerated(DatabaseGeneratedOption.Computed)]
    [Column("xmin", TypeName = "xid")]
    public uint ConcurrencyToken { get; set; }
    #endregion

    #region Constructors
    /// <summary>
    /// Creates a new instance of a BaseEntity class.
    /// Initializes the default values.
    /// </summary>
    public BaseEntity() { }
    #endregion
}
