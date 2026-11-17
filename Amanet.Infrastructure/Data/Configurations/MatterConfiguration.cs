using Amanet.Core.Entities;
using Microsoft.EntityFrameworkCore;
using Microsoft.EntityFrameworkCore.Metadata.Builders;

namespace Amanet.Infrastructure.Data.Configurations;

public sealed class MatterConfiguration : IEntityTypeConfiguration<Matter>
{
    public void Configure(EntityTypeBuilder<Matter> builder)
    {
        builder.Property(matter => matter.ReferenceNumber).IsRequired().HasMaxLength(50);
        builder.HasIndex(matter => matter.ReferenceNumber).IsUnique();
        builder.Property(matter => matter.Title).IsRequired().HasMaxLength(200);
        builder.Property(matter => matter.Description).IsRequired().HasMaxLength(5000);
        builder.Property(matter => matter.Status).HasConversion<string>().HasMaxLength(20);
        builder.Property(matter => matter.Priority).HasConversion<string>().HasMaxLength(10);
        builder.HasOne(matter => matter.PrimaryClient).WithMany()
            .HasForeignKey(matter => matter.PrimaryClientId).OnDelete(DeleteBehavior.Restrict);
        builder.Property(matter => matter.Version).IsRowVersion();
        builder.ToTable("Matters", table => table.HasCheckConstraint("CK_Matters_TargetCloseDate",
            "\"TargetCloseDate\" IS NULL OR \"TargetCloseDate\" >= \"OpenedDate\""));
    }
}
