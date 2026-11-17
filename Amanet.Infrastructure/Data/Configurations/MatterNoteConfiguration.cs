using Amanet.Core.Entities;
using Amanet.Infrastructure.Identity;
using Microsoft.EntityFrameworkCore;
using Microsoft.EntityFrameworkCore.Metadata.Builders;

namespace Amanet.Infrastructure.Data.Configurations;

public sealed class MatterNoteConfiguration : IEntityTypeConfiguration<MatterNote>
{
    public void Configure(EntityTypeBuilder<MatterNote> builder)
    {
        builder.Property(note => note.Text).IsRequired().HasMaxLength(10000);
        builder.HasOne<ApplicationUser>().WithMany().HasForeignKey(note => note.AuthorId)
            .OnDelete(DeleteBehavior.Restrict);
        builder.HasOne(item => item.Matter).WithMany().HasForeignKey(item => item.MatterId)
            .OnDelete(DeleteBehavior.Restrict);
    }
}
