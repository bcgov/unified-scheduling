using Microsoft.EntityFrameworkCore;
using Microsoft.EntityFrameworkCore.Metadata;
using Microsoft.EntityFrameworkCore.Metadata.Builders;
using Unified.Db.Models;
using Unified.Db.Models.Abstract;

//Thanks to PIMS for this.
namespace Unified.Db.Configuration
{
    /// <summary>
    /// BaseEntityConfiguration class, provides a way to configure base entity in the database.
    ///</summary>
    public abstract class BaseEntityConfiguration<TBase> : IEntityTypeConfiguration<TBase>
        where TBase : BaseEntity
    {
        #region Methods
        protected void BaseConfigure(EntityTypeBuilder<TBase> builder)
        {
            builder
                .HasOne(m => m.CreatedBy)
                .WithMany()
                .HasForeignKey(m => m.CreatedById)
                .OnDelete(DeleteBehavior.SetNull);
            builder
                .HasOne(m => m.UpdatedBy)
                .WithMany()
                .HasForeignKey(m => m.UpdatedById)
                .OnDelete(DeleteBehavior.SetNull);
            builder
                .Property(b => b.CreatedOn)
                .HasDefaultValueSql("now()")
                .Metadata.SetAfterSaveBehavior(PropertySaveBehavior.Ignore);
        }

        public virtual void Configure(EntityTypeBuilder<TBase> builder)
        {
            BaseConfigure(builder);
        }
        #endregion
    }
}
