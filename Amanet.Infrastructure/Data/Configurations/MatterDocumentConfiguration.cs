using Amanet.Core.Entities;
using Amanet.Infrastructure.Identity;
using Microsoft.EntityFrameworkCore;
using Microsoft.EntityFrameworkCore.Metadata;
using Microsoft.EntityFrameworkCore.Metadata.Builders;

namespace Amanet.Infrastructure.Data.Configurations;

public sealed class MatterDocumentConfiguration : IEntityTypeConfiguration<MatterDocument>
{
    public void Configure(EntityTypeBuilder<MatterDocument> builder)
    {
        builder.Property(document => document.Title).IsRequired().HasMaxLength(200);
        builder.Property(document => document.Category).HasMaxLength(100);
        builder.Property(document => document.Classification).HasConversion<string>().HasMaxLength(20);
        builder.HasOne(document => document.Matter).WithMany().HasForeignKey(document => document.MatterId)
            .OnDelete(DeleteBehavior.Restrict);
    }
}

public sealed class DocumentVersionConfiguration : IEntityTypeConfiguration<DocumentVersion>
{
    public void Configure(EntityTypeBuilder<DocumentVersion> builder)
    {
        builder.Property(version => version.OriginalFileName).IsRequired().HasMaxLength(255);
        builder.Property(version => version.ContentType).IsRequired().HasMaxLength(100);
        builder.Property(version => version.Sha256).IsRequired().HasMaxLength(64);
        builder.Property(version => version.StorageKey).IsRequired().HasMaxLength(36);
        builder.Property(version => version.Source).IsRequired().HasMaxLength(30);
        builder.HasIndex(version => new { version.DocumentId, version.VersionNumber }).IsUnique();
        builder.HasIndex(version => version.StorageKey).IsUnique();
        builder.HasOne(version => version.Document).WithMany(document => document.Versions)
            .HasForeignKey(version => version.DocumentId).OnDelete(DeleteBehavior.Restrict);
        builder.HasOne<ApplicationUser>().WithMany().HasForeignKey(version => version.UploaderId)
            .OnDelete(DeleteBehavior.Restrict);
        builder.ToTable("DocumentVersions", table =>
        {
            table.HasCheckConstraint("CK_DocumentVersions_Number", "\"VersionNumber\" > 0");
            table.HasCheckConstraint("CK_DocumentVersions_Length", "\"ByteLength\" > 0 AND \"ByteLength\" <= 26214400");
        });
        foreach (var property in builder.Metadata.GetProperties())
            property.SetAfterSaveBehavior(PropertySaveBehavior.Throw);
    }
}
