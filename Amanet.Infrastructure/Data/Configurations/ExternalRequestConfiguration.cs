using Amanet.Core.Entities;
using Amanet.Infrastructure.Identity;
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
        builder.Property(request => request.RecipientName).IsRequired().HasMaxLength(200);
        builder.Property(request => request.Instructions).IsRequired().HasMaxLength(5000);
        builder.Property(request => request.UploadStatus).HasConversion<string>().HasMaxLength(30);
        builder.HasIndex(request => new { request.MatterId, request.UploadStatus });
        builder.HasOne(request => request.UploadedDocument).WithOne().HasForeignKey<ExternalRequest>(request => request.UploadedDocumentId)
            .OnDelete(DeleteBehavior.Restrict);
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

public sealed class DocumentSubmissionConfiguration : IEntityTypeConfiguration<DocumentSubmission>
{
    public void Configure(EntityTypeBuilder<DocumentSubmission> builder)
    {
        builder.HasKey(submission => submission.DocumentVersionId);
        builder.HasOne(submission => submission.DocumentVersion).WithOne()
            .HasForeignKey<DocumentSubmission>(submission => submission.DocumentVersionId).OnDelete(DeleteBehavior.Restrict);
        builder.HasOne(submission => submission.ExternalRequest).WithMany(request => request.Submissions)
            .HasForeignKey(submission => submission.ExternalRequestId).OnDelete(DeleteBehavior.Restrict);
        builder.HasOne<ApplicationUser>().WithMany().HasForeignKey(submission => submission.ReviewerId)
            .OnDelete(DeleteBehavior.Restrict);
        builder.Property(submission => submission.Decision).HasConversion<string>().HasMaxLength(30);
        builder.Property(submission => submission.CorrectionExplanation).HasMaxLength(5000);
        builder.Property(submission => submission.DocumentVersionId).Metadata.SetAfterSaveBehavior(PropertySaveBehavior.Throw);
        builder.Property(submission => submission.ExternalRequestId).Metadata.SetAfterSaveBehavior(PropertySaveBehavior.Throw);
        builder.ToTable("DocumentSubmissions", table => table.HasCheckConstraint("CK_DocumentSubmissions_Review",
            "\"Decision\" <> 'CorrectionsRequested' OR " +
            "(\"CorrectionExplanation\" IS NOT NULL AND length(btrim(\"CorrectionExplanation\")) > 0)"));
    }
}

public sealed class AdminNotificationConfiguration : IEntityTypeConfiguration<AdminNotification>
{
    public void Configure(EntityTypeBuilder<AdminNotification> builder)
    {
        builder.Property(notification => notification.Message).IsRequired().HasMaxLength(500);
        builder.HasIndex(notification => notification.CreatedAtUtc);
        builder.HasOne(notification => notification.Matter).WithMany().HasForeignKey(notification => notification.MatterId)
            .OnDelete(DeleteBehavior.Restrict);
        builder.HasOne(notification => notification.ExternalRequest).WithMany().HasForeignKey(notification => notification.ExternalRequestId)
            .OnDelete(DeleteBehavior.Restrict);
        builder.HasOne(notification => notification.DocumentVersion).WithMany().HasForeignKey(notification => notification.DocumentVersionId)
            .OnDelete(DeleteBehavior.Restrict);
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
