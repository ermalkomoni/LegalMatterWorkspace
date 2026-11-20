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
    IReadOnlyList<DocumentSubmissionDto> Submissions, int? SourceVersionId, SignerKind? SignerKind,
    DateTimeOffset? ViewedAtUtc, DateTimeOffset? StartedAtUtc, DateTimeOffset? ReplacedAtUtc, int? SigningDocumentId);

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
    string RecipientName, string Instructions, DocumentRequestStatus? UploadStatus, string? CorrectionExplanation,
    string? ConsentText = null, string? SourceSha256 = null, DateTimeOffset? SentAtUtc = null,
    DateTimeOffset? ViewedAtUtc = null, DateTimeOffset? StartedAtUtc = null, DateTimeOffset? SignedAtUtc = null);

public interface IExternalRequestService
{
    // Create/resend commit before sending and must be called outside caller-owned database transactions.
    Task<ExternalRequestDto> CreateAsync(int matterId, CreateExternalRequest input, CancellationToken cancellationToken);
    Task<IReadOnlyList<ExternalRequestDto>> ListAsync(int? matterId, DocumentRequestStatus? status, CancellationToken cancellationToken);
    Task<ExternalRequestDto> GetAsync(int matterId, Guid requestId, CancellationToken cancellationToken);
    Task<ExternalRequestDto> ResendAsync(int matterId, Guid requestId, EmailTemplate? template, CancellationToken cancellationToken);
    Task<ExternalRequestDto> RevokeAsync(int matterId, Guid requestId, CancellationToken cancellationToken);
    Task<PublicRequestContext> GetContextAsync(Guid requestId, ExternalAction action, string? token, CancellationToken cancellationToken);
    Task<DocumentFile> OpenPdfAsync(Guid requestId, string? token, CancellationToken cancellationToken);

    // Returns a fresh, untracked snapshot so validation never trusts cached link/matter state.
    // Workflow services call this for every public operation. Signing and document-review services
    // own completion; a link alone can never mark a document as signed.
    Task<ExternalRequest> ValidateAsync(Guid requestId, ExternalAction action, string? token,
        bool forSubmission, CancellationToken cancellationToken);
}

public interface IClientDocumentRequestService
{
    // These operations own their commit; call outside caller-owned or ambient database transactions.
    Task<PublicRequestContext> UploadAsync(Guid requestId, string? token, Stream content, string fileName,
        CancellationToken cancellationToken);
    Task<ExternalRequestDto> ReviewAsync(int matterId, Guid requestId, int versionId,
        ReviewSubmissionRequest input, int reviewerId, CancellationToken cancellationToken);
}

public enum AdminNotificationKind
{
    DocumentResponse, SignatureActivity, AwaitingUpload, SubmissionAwaitingReview,
    CorrectionsRequired, SignaturePending, OverdueTask, OverdueDeadline
}

// Live pending actions have no persisted notification ID. Overdue work has a due date instead of an event timestamp.
public sealed record AdminNotificationDto(int? Id, int MatterId, Guid? RequestId, int? DocumentVersionId,
    string Message, DateTimeOffset? CreatedAtUtc, AdminNotificationKind Kind, bool IsActionable,
    int? TaskId, int? DeadlineId, DateOnly? DueDate);

public interface IAdminNotificationService
{
    // Adds to the current unit of work; the workflow owns the save/transaction.
    void RecordSubmission(ExternalRequest request, DocumentVersion version);
    void RecordSigning(ExternalRequest request, DocumentVersion version, string message);
    Task<PagedResult<AdminNotificationDto>> ListAsync(NotificationQuery query, CancellationToken cancellationToken);
}

public interface IEmailSender
{
    // The body is transient. Implementations must never log or persist it or provider exception details.
    Task SendAsync(string recipient, string subject, string textBody, CancellationToken cancellationToken);
}

public static class SigningConsent
{
    public const string Text = "I have reviewed this document and agree to sign it electronically using my typed name.";
}

public class SignRequest
{
    [Required, StringLength(200)]
    public string TypedName { get; init; } = string.Empty;
    [Required]
    public bool? ConsentAccepted { get; init; }
}

public sealed class AdminSignRequest : SignRequest
{
    [Range(1, int.MaxValue)]
    public int SourceVersionId { get; init; }
}

public sealed record SignatureDto(Guid RequestId, int DocumentId, int SourceVersionId, string SourceSha256,
    int FinalVersionId, int FinalVersionNumber, string FinalFileName, string FinalSha256,
    SignerKind SignerKind, string SignerName, string SignerEmail, int? AdminSignerId,
    DateTimeOffset SignedAtUtc, string ConsentText);

public interface ISigningService
{
    Task<SignatureDto> SignAdminAsync(int matterId, int documentId, AdminSignRequest input, int adminId, CancellationToken cancellationToken);
    Task<SignatureDto> SignClientAsync(Guid requestId, string? token, SignRequest input, CancellationToken cancellationToken);
    Task<PublicRequestContext> AdvanceAsync(Guid requestId, string? token, RequestProgress progress, CancellationToken cancellationToken);
    Task<SignatureDto> GetAsync(int matterId, int documentId, CancellationToken cancellationToken);
}
