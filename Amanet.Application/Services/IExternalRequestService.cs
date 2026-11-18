using System.ComponentModel.DataAnnotations;
using Amanet.Application.DTOs.Documents;
using Amanet.Application.DTOs;
using Amanet.Core.Entities;

namespace Amanet.Application.Services;

public sealed class CreateExternalRequest
{
    [EnumDataType(typeof(ExternalAction))]
    public ExternalAction Action { get; init; }
    [Required, EmailAddress, StringLength(254)]
    public string RecipientEmail { get; init; } = string.Empty;
    [Required, StringLength(200)]
    public string Title { get; init; } = string.Empty;
    [Range(1, int.MaxValue)]
    public int? DocumentVersionId { get; init; }

    [StringLength(200)]
    public string RecipientName { get; init; } = string.Empty;
    [StringLength(5000)]
    public string Instructions { get; init; } = string.Empty;
    public DateTimeOffset? ExpiresAtUtc { get; init; }
}

public sealed class ResendExternalRequest
{
    // Null chooses the invitation appropriate to the request's action.
    [EnumDataType(typeof(EmailTemplate))]
    public EmailTemplate? Template { get; init; }
}

public sealed record DeliveryAttemptDto(Guid Id, EmailTemplate Template, DeliveryStatus Status,
    DateTimeOffset StartedAtUtc, DateTimeOffset? FinishedAtUtc, string? FailureCode);
public sealed record ExternalRequestDto(Guid Id, int MatterId, ExternalAction Action, string RecipientEmail,
    string Title, RequestProgress Progress, DateTimeOffset ExpiresAtUtc, DateTimeOffset? RevokedAtUtc,
    DateTimeOffset? CompletedAtUtc, IReadOnlyList<DeliveryAttemptDto> DeliveryAttempts,
    string RecipientName, string Instructions, DocumentRequestStatus? UploadStatus, int? UploadedDocumentId,
    IReadOnlyList<DocumentSubmissionDto> Submissions);

public sealed record DocumentSubmissionDto(int DocumentVersionId, int DocumentId, int VersionNumber,
    string FileName, DateTimeOffset SubmittedAtUtc, SubmissionDecision? Decision,
    string? CorrectionExplanation, int? ReviewerId, DateTimeOffset? ReviewedAtUtc);

public sealed class ReviewSubmissionRequest
{
    [Required, EnumDataType(typeof(SubmissionDecision))]
    public SubmissionDecision? Decision { get; init; }
    [StringLength(5000)]
    public string? Explanation { get; init; }
}

// Deliberate allowlist: no matter/client navigation, notes, recipient address or storage identifiers.
public sealed record PublicRequestContext(Guid RequestId, ExternalAction Action, string Title,
    RequestProgress Progress, DateTimeOffset ExpiresAtUtc, bool CanSubmit, bool CanReadPdf,
    string RecipientName, string Instructions, DocumentRequestStatus? UploadStatus, string? CorrectionExplanation);

public interface IExternalRequestService
{
    // Create/resend commit before SMTP and must be called outside caller-owned database transactions.
    Task<ExternalRequestDto> CreateAsync(int matterId, CreateExternalRequest input, CancellationToken cancellationToken);
    Task<IReadOnlyList<ExternalRequestDto>> ListAsync(int? matterId, DocumentRequestStatus? status, CancellationToken cancellationToken);
    Task<ExternalRequestDto> GetAsync(int matterId, Guid requestId, CancellationToken cancellationToken);
    Task<ExternalRequestDto> ResendAsync(int matterId, Guid requestId, EmailTemplate? template, CancellationToken cancellationToken);
    Task<ExternalRequestDto> RevokeAsync(int matterId, Guid requestId, CancellationToken cancellationToken);
    Task<PublicRequestContext> GetContextAsync(Guid requestId, ExternalAction action, string? token, CancellationToken cancellationToken);
    Task<DocumentFile> OpenPdfAsync(Guid requestId, string? token, CancellationToken cancellationToken);

    // Returns a fresh, untracked snapshot so validation never trusts cached link/matter state.
    // Workflow services call this for every public operation. CompleteAsync completes signing only;
    // document requests finish through the version-specific review operation.
    Task<ExternalRequest> ValidateAsync(Guid requestId, ExternalAction action, string? token,
        bool forSubmission, CancellationToken cancellationToken);
    Task CompleteAsync(Guid requestId, ExternalAction action, string? token, int? signedDocumentVersionId,
        CancellationToken cancellationToken);
}

public interface IClientDocumentRequestService
{
    // These operations own their commit; call outside caller-owned or ambient database transactions.
    Task<PublicRequestContext> UploadAsync(Guid requestId, string? token, Stream content, string fileName,
        CancellationToken cancellationToken);
    Task<ExternalRequestDto> ReviewAsync(int matterId, Guid requestId, int versionId,
        ReviewSubmissionRequest input, int reviewerId, CancellationToken cancellationToken);
}

public sealed record AdminNotificationDto(int Id, int MatterId, Guid RequestId, int DocumentVersionId,
    string Message, DateTimeOffset CreatedAtUtc);

public interface IAdminNotificationService
{
    // Adds to the current unit of work; the workflow owns the save/transaction.
    void RecordSubmission(ExternalRequest request, DocumentVersion version);
    Task<PagedResult<AdminNotificationDto>> ListAsync(ListQuery query, CancellationToken cancellationToken);
}

public interface IEmailSender
{
    // The body is transient. Implementations must never log or persist it or SMTP exception details.
    Task SendAsync(string recipient, string subject, string textBody, CancellationToken cancellationToken);
}
