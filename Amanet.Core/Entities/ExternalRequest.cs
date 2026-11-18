namespace Amanet.Core.Entities;

public enum ExternalAction { Upload, Sign }
public enum RequestProgress { Pending, Viewed, Started, Completed }
public enum EmailTemplate { DocumentRequest, SignatureInvitation, Correction, Reminder }
public enum DeliveryStatus { Pending, Sent, Failed }

// One current link per request. Rotation replaces its hash; old tokens cannot be recovered.
public sealed class ExternalRequest
{
    public Guid Id { get; set; }
    public int MatterId { get; set; }
    public Matter Matter { get; set; } = null!;
    public ExternalAction Action { get; set; }
    public string RecipientEmail { get; set; } = string.Empty;
    public string Title { get; set; } = string.Empty;
    public RequestProgress Progress { get; set; }
    public int? DocumentVersionId { get; set; }
    public int? SignedDocumentVersionId { get; set; }
    public string TokenHash { get; set; } = string.Empty;
    public DateTimeOffset CreatedAtUtc { get; set; }
    public DateTimeOffset ExpiresAtUtc { get; set; }
    public DateTimeOffset? RevokedAtUtc { get; set; }
    public DateTimeOffset? CompletedAtUtc { get; set; }
    public uint Version { get; set; }
    public ICollection<EmailDeliveryAttempt> DeliveryAttempts { get; set; } = [];
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
