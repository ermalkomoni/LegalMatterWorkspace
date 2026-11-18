using Amanet.Application.DTOs;
using Amanet.Application.Services;
using Amanet.Core.Entities;
using Amanet.Core.Enums;
using Amanet.Infrastructure.Data;
using Microsoft.EntityFrameworkCore;

namespace Amanet.Infrastructure.Services;

public sealed class AdminNotificationService(ApplicationDbContext db) : IAdminNotificationService
{
    public void RecordSubmission(ExternalRequest request, DocumentVersion version)
    {
        RecordSigning(request, version, $"PDF submitted for review: {request.Title} (version {version.VersionNumber}).");
    }

    public void RecordSigning(ExternalRequest request, DocumentVersion version, string message)
    {
        db.AdminNotifications.Add(new AdminNotification
        {
            Matter = request.Matter, ExternalRequest = request, DocumentVersion = version,
            Message = message, CreatedAtUtc = DateTimeOffset.UtcNow
        });
    }

    public async Task<PagedResult<AdminNotificationDto>> ListAsync(ListQuery query, CancellationToken cancellationToken)
    {
        var now = DateTimeOffset.UtcNow;
        var today = DateOnly.FromDateTime(now.UtcDateTime);
        var activeRequests = db.ExternalRequests.AsNoTracking().Where(request =>
            request.Matter.ArchivedAtUtc == null && request.RevokedAtUtc == null &&
            request.ReplacedAtUtc == null && request.ExpiresAtUtc > now && request.CompletedAtUtc == null &&
            request.Progress != RequestProgress.Completed && request.Progress != RequestProgress.Signed);

        // Saved events remain historical. Current actions are derived from committed workflow state,
        // so review, completion, revocation and expiry do not leave stale actionable notifications.
        var history = db.AdminNotifications.AsNoTracking().Select(notification => new
        {
            Id = (int?)notification.Id,
            notification.MatterId,
            RequestId = (Guid?)notification.ExternalRequestId,
            DocumentVersionId = (int?)notification.DocumentVersionId,
            notification.Message,
            CreatedAtUtc = (DateTimeOffset?)notification.CreatedAtUtc,
            Kind = notification.ExternalRequest.Action == ExternalAction.Upload
                ? AdminNotificationKind.DocumentResponse : AdminNotificationKind.SignatureActivity,
            IsActionable = false,
            TaskId = (int?)null,
            DeadlineId = (int?)null,
            DueDate = (DateOnly?)null
        });
        var requirements = activeRequests.Where(request => request.Action == ExternalAction.Upload &&
                (request.UploadStatus == DocumentRequestStatus.AwaitingUpload ||
                 request.UploadStatus == DocumentRequestStatus.Submitted ||
                 request.UploadStatus == DocumentRequestStatus.CorrectionsRequested) ||
                request.Action == ExternalAction.Sign && request.SignerKind == SignerKind.ExternalClient &&
                request.SigningDocumentId != null &&
                db.DocumentVersions.Any(version => version.Id == request.DocumentVersionId &&
                    version.DocumentId == request.SigningDocumentId && version.Document.MatterId == request.MatterId &&
                    version.Document.SignedAtUtc == null))
            .Select(request => new
            {
                Id = (int?)null,
                request.MatterId,
                RequestId = (Guid?)request.Id,
                DocumentVersionId = request.Action == ExternalAction.Sign ? request.DocumentVersionId :
                    request.Submissions.OrderByDescending(submission => submission.DocumentVersion.VersionNumber)
                        .Select(submission => (int?)submission.DocumentVersionId).FirstOrDefault(),
                Message = request.Action == ExternalAction.Sign ? "Signature pending: " + request.Title :
                    request.UploadStatus == DocumentRequestStatus.Submitted ? "Submission awaiting review: " + request.Title :
                    request.UploadStatus == DocumentRequestStatus.CorrectionsRequested ? "Corrections required: " + request.Title :
                    "Document upload outstanding: " + request.Title,
                CreatedAtUtc = request.Submissions.OrderByDescending(submission => submission.DocumentVersion.VersionNumber)
                    .Select(submission => submission.ReviewedAtUtc
                        ?? (DateTimeOffset?)submission.DocumentVersion.UploadedAtUtc)
                    .FirstOrDefault() ?? (DateTimeOffset?)request.CreatedAtUtc,
                Kind = request.Action == ExternalAction.Sign ? AdminNotificationKind.SignaturePending :
                    request.UploadStatus == DocumentRequestStatus.Submitted ? AdminNotificationKind.SubmissionAwaitingReview :
                    request.UploadStatus == DocumentRequestStatus.CorrectionsRequested ? AdminNotificationKind.CorrectionsRequired :
                    AdminNotificationKind.AwaitingUpload,
                IsActionable = true,
                TaskId = (int?)null,
                DeadlineId = (int?)null,
                DueDate = (DateOnly?)null
            });
        var tasks = db.MatterTasks.AsNoTracking().Where(task => task.Matter.ArchivedAtUtc == null &&
                task.Status == WorkItemStatus.Pending && task.DueDate < today)
            .Select(task => new
            {
                Id = (int?)null,
                task.MatterId,
                RequestId = (Guid?)null,
                DocumentVersionId = (int?)null,
                Message = "Overdue task: " + task.Title,
                CreatedAtUtc = (DateTimeOffset?)null,
                Kind = AdminNotificationKind.OverdueTask,
                IsActionable = true,
                TaskId = (int?)task.Id,
                DeadlineId = (int?)null,
                task.DueDate
            });
        var deadlines = db.MatterDeadlines.AsNoTracking().Where(deadline => deadline.Matter.ArchivedAtUtc == null &&
                deadline.Status == WorkItemStatus.Pending && deadline.Date < today)
            .Select(deadline => new
            {
                Id = (int?)null,
                deadline.MatterId,
                RequestId = (Guid?)null,
                DocumentVersionId = (int?)null,
                Message = "Overdue deadline: " + deadline.Title,
                CreatedAtUtc = (DateTimeOffset?)null,
                Kind = AdminNotificationKind.OverdueDeadline,
                IsActionable = true,
                TaskId = (int?)null,
                DeadlineId = (int?)deadline.Id,
                DueDate = (DateOnly?)deadline.Date
            });
        var notifications = history.Concat(requirements).Concat(tasks).Concat(deadlines);
        if (!string.IsNullOrWhiteSpace(query.Search))
        {
            var term = query.Search.Trim().ToLowerInvariant();
            notifications = notifications.Where(notification => notification.Message.ToLower().Contains(term));
        }
        var count = await notifications.CountAsync(cancellationToken);
        var offset = ((long)query.Page - 1) * query.PageSize;
        var items = offset > int.MaxValue ? [] : await notifications.OrderByDescending(notification => notification.IsActionable)
            .ThenBy(notification => notification.DueDate ?? DateOnly.MaxValue)
            .ThenByDescending(notification => notification.CreatedAtUtc)
            .ThenBy(notification => notification.Kind).ThenBy(notification => notification.MatterId)
            .ThenBy(notification => notification.RequestId).ThenBy(notification => notification.TaskId)
            .ThenBy(notification => notification.DeadlineId).ThenByDescending(notification => notification.Id)
            .Skip((int)offset).Take(query.PageSize)
            .Select(notification => new AdminNotificationDto(notification.Id, notification.MatterId,
                notification.RequestId, notification.DocumentVersionId, notification.Message, notification.CreatedAtUtc,
                notification.Kind, notification.IsActionable, notification.TaskId, notification.DeadlineId, notification.DueDate))
            .ToArrayAsync(cancellationToken);
        return new PagedResult<AdminNotificationDto>(items, count, query.Page, query.PageSize);
    }
}
