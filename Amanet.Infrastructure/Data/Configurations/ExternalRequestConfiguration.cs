using Amanet.Core.Entities;
using Microsoft.EntityFrameworkCore;
using Microsoft.EntityFrameworkCore.Metadata;
using Microsoft.EntityFrameworkCore.Metadata.Builders;

namespace Amanet.Infrastructure.Data.Configurations;

public sealed class ExternalRequestConfiguration : IEntityTypeConfiguration<ExternalRequest>
{
    public void Configure(EntityTypeBuilder<ExternalRequest> builder)
    {
        builder.HasOne(request => request.Matter).WithMany().HasForeignKey(request => request.MatterId)
            .OnDelete(DeleteBehavior.Restrict);
        builder.Property(request => request.Action).HasConversion<string>().HasMaxLength(10);
        builder.Property(request => request.Progress).HasConversion<string>().HasMaxLength(20);
        builder.Property(request => request.RecipientEmail).IsRequired().HasMaxLength(254);
        builder.Property(request => request.Title).IsRequired().HasMaxLength(200);
        builder.Property(request => request.TokenHash).IsRequired().HasMaxLength(64);
        builder.Property(request => request.Version).IsRowVersion();
        builder.HasOne<DocumentVersion>().WithMany().HasForeignKey(request => request.DocumentVersionId)
            .OnDelete(DeleteBehavior.Restrict);
        builder.HasOne<DocumentVersion>().WithMany().HasForeignKey(request => request.SignedDocumentVersionId)
            .OnDelete(DeleteBehavior.Restrict);
        builder.Property(request => request.MatterId).Metadata.SetAfterSaveBehavior(PropertySaveBehavior.Throw);
        builder.Property(request => request.Action).Metadata.SetAfterSaveBehavior(PropertySaveBehavior.Throw);
        builder.Property(request => request.DocumentVersionId).Metadata.SetAfterSaveBehavior(PropertySaveBehavior.Throw);
    }
}

public sealed class EmailDeliveryAttemptConfiguration : IEntityTypeConfiguration<EmailDeliveryAttempt>
{
    public void Configure(EntityTypeBuilder<EmailDeliveryAttempt> builder)
    {
        builder.HasOne(attempt => attempt.ExternalRequest).WithMany(request => request.DeliveryAttempts)
            .HasForeignKey(attempt => attempt.ExternalRequestId).OnDelete(DeleteBehavior.Restrict);
        builder.Property(attempt => attempt.Template).HasConversion<string>().HasMaxLength(30);
        builder.Property(attempt => attempt.Status).HasConversion<string>().HasMaxLength(10);
        builder.Property(attempt => attempt.FailureCode).HasMaxLength(40);
        builder.HasIndex(attempt => new { attempt.ExternalRequestId, attempt.StartedAtUtc });
    }
}
