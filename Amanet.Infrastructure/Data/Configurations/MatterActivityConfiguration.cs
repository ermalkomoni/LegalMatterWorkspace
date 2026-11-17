using Amanet.Core.Entities;
using Microsoft.EntityFrameworkCore;
using Microsoft.EntityFrameworkCore.Metadata.Builders;

namespace Amanet.Infrastructure.Data.Configurations;

public sealed class MatterActivityConfiguration : IEntityTypeConfiguration<MatterActivity>
{
    public void Configure(EntityTypeBuilder<MatterActivity> builder)
    {
        builder.Property(activity => activity.Type).HasConversion<string>().HasMaxLength(30);
        builder.Property(activity => activity.Description).IsRequired().HasMaxLength(1000);
        builder.HasOne(activity => activity.Matter).WithMany().HasForeignKey(activity => activity.MatterId)
            .OnDelete(DeleteBehavior.Restrict);
        builder.HasIndex(activity => new { activity.MatterId, activity.OccurredAtUtc });
    }
}
