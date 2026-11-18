using Amanet.Application.DTOs;
using Amanet.Application.Services;
using Amanet.Core.Entities;
using Amanet.Infrastructure.Data;
using Microsoft.EntityFrameworkCore;

namespace Amanet.Infrastructure.Services;

public sealed class AdminNotificationService(ApplicationDbContext db) : IAdminNotificationService
{
    public void RecordSubmission(ExternalRequest request, DocumentVersion version)
    {
        db.AdminNotifications.Add(new AdminNotification
        {
            Matter = request.Matter, ExternalRequest = request, DocumentVersion = version,
            Message = $"PDF submitted for review: {request.Title} (version {version.VersionNumber}).",
            CreatedAtUtc = version.UploadedAtUtc
        });
    }

    public async Task<PagedResult<AdminNotificationDto>> ListAsync(ListQuery query, CancellationToken cancellationToken)
    {
        var notifications = db.AdminNotifications.AsNoTracking();
        if (!string.IsNullOrWhiteSpace(query.Search))
        {
            var term = query.Search.Trim().ToLowerInvariant();
            notifications = notifications.Where(notification => notification.Message.ToLower().Contains(term));
        }
        var count = await notifications.CountAsync(cancellationToken);
        var offset = ((long)query.Page - 1) * query.PageSize;
        var items = offset > int.MaxValue ? [] : await notifications.OrderByDescending(notification => notification.CreatedAtUtc)
            .ThenByDescending(notification => notification.Id).Skip((int)offset).Take(query.PageSize)
            .Select(notification => new AdminNotificationDto(notification.Id, notification.MatterId,
                notification.ExternalRequestId, notification.DocumentVersionId, notification.Message, notification.CreatedAtUtc))
            .ToArrayAsync(cancellationToken);
        return new PagedResult<AdminNotificationDto>(items, count, query.Page, query.PageSize);
    }
}
