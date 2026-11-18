namespace Amanet.Core.Entities;

public enum ExternalAction { Upload, Sign }
// Pending/Completed remain for upload-request compatibility; signing uses Sent/Viewed/Started/Signed.
public enum RequestProgress { Pending, Viewed, Started, Completed, Sent, Signed }
public enum SignerKind { ExternalClient, Admin }
public enum EmailTemplate { DocumentRequest, SignatureInvitation, Correction, Reminder }
public enum DeliveryStatus { Pending, Sent, Failed }
public enum DocumentRequestStatus { AwaitingUpload, Submitted, CorrectionsRequested, Accepted }
public enum SubmissionDecision { Accepted, CorrectionsRequested }

// One current link per request. Rotation replaces its hash; old tokens cannot be recovered.
public sealed class ExternalRequest
{
    public Guid Id { get; set; }
    public int MatterId { get; set; }
    public Matter Matter { get; set; } = null!;
    public ExternalAction Action { get; set; }
    public string RecipientEmail { get; set; } = string.Empty;
    public string Title { get; set; } = string.Empty;
    public string RecipientName { get; set; } = string.Empty;
    public string Instructions { get; set; } = string.Empty;
    public DocumentRequestStatus? UploadStatus { get; set; }
    public int? UploadedDocumentId { get; set; }
    public MatterDocument? UploadedDocument { get; set; }
    public ICollection<DocumentSubmission> Submissions { get; set; } = [];
    public RequestProgress Progress { get; set; }
    public int? DocumentVersionId { get; set; }
    public int? SignedDocumentVersionId { get; set; }
    public DocumentVersion? SignedDocumentVersion { get; set; }
    public SignerKind? SignerKind { get; set; }
    public string? SigningConsentText { get; set; }
    public int? AdminSignerId { get; set; }
    // A unique nullable slot: only the current workflow holds its logical document ID.
    public int? SigningDocumentId { get; set; }
    public DateTimeOffset? ReplacedAtUtc { get; set; }
    public DateTimeOffset? ViewedAtUtc { get; set; }
    public DateTimeOffset? StartedAtUtc { get; set; }
    public string TokenHash { get; set; } = string.Empty;
    public DateTimeOffset CreatedAtUtc { get; set; }
    public DateTimeOffset ExpiresAtUtc { get; set; }
    public DateTimeOffset? RevokedAtUtc { get; set; }
    public DateTimeOffset? CompletedAtUtc { get; set; }
    public uint Version { get; set; }
    public ICollection<EmailDeliveryAttempt> DeliveryAttempts { get; set; } = [];
}

public sealed class SignedDocument
{
    public Guid RequestId { get; set; }
    public ExternalRequest Request { get; set; } = null!;
    public int DocumentId { get; set; }
    public MatterDocument Document { get; set; } = null!;
    public int SourceVersionId { get; set; }
    public DocumentVersion SourceVersion { get; set; } = null!;
    public int FinalVersionId { get; set; }
    public DocumentVersion FinalVersion { get; set; } = null!;
    public SignerKind SignerKind { get; set; }
    public string SignerName { get; set; } = string.Empty;
    public string SignerEmail { get; set; } = string.Empty;
    public int? AdminSignerId { get; set; }
    public DateTimeOffset SignedAtUtc { get; set; }
    public string ConsentText { get; set; } = string.Empty;
    public string SourceSha256 { get; set; } = string.Empty;
}

// One immutable submitted version, with at most one review decision. Corrections create a new row/version.
public sealed class DocumentSubmission
{
    public int DocumentVersionId { get; set; }
    public DocumentVersion DocumentVersion { get; set; } = null!;
    public Guid ExternalRequestId { get; set; }
    public ExternalRequest ExternalRequest { get; set; } = null!;
    public SubmissionDecision? Decision { get; set; }
    public string? CorrectionExplanation { get; set; }
    public int? ReviewerId { get; set; }
    public DateTimeOffset? ReviewedAtUtc { get; set; }
}

public sealed class AdminNotification
{
    public int Id { get; set; }
    public int MatterId { get; set; }
    public Matter Matter { get; set; } = null!;
    public Guid ExternalRequestId { get; set; }
    public ExternalRequest ExternalRequest { get; set; } = null!;
    public int DocumentVersionId { get; set; }
    public DocumentVersion DocumentVersion { get; set; } = null!;
    public string Message { get; set; } = string.Empty;
    public DateTimeOffset CreatedAtUtc { get; set; }
}

// No subject, body, URL, token, SMTP response, or exception message is persisted here.
public sealed class EmailDeliveryAttempt
{
    public Guid Id { get; set; }
    public Guid ExternalRequestId { get; set; }
    public ExternalRequest ExternalRequest { get; set; } = null!;
    public EmailTemplate Template { get; set; }
    public DeliveryStatus Status { get; set; }
    public DateTimeOffset StartedAtUtc { get; set; }
    public DateTimeOffset? FinishedAtUtc { get; set; }
    public string? FailureCode { get; set; }
}
