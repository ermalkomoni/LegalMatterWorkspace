using Amanet.Core.Entities;
using Microsoft.EntityFrameworkCore;
using Microsoft.EntityFrameworkCore.Metadata.Builders;

namespace Amanet.Infrastructure.Data.Configurations;

public sealed class MatterDeadlineConfiguration : IEntityTypeConfiguration<MatterDeadline>
{
    public void Configure(EntityTypeBuilder<MatterDeadline> builder)
    {
        builder.Property(item => item.Title).IsRequired().HasMaxLength(200);
        builder.Property(item => item.Status).HasConversion<string>().HasMaxLength(20);
        builder.HasOne(item => item.Matter).WithMany().HasForeignKey(item => item.MatterId)
            .OnDelete(DeleteBehavior.Restrict);
    }
}
