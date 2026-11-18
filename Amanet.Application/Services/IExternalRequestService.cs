using System.ComponentModel.DataAnnotations;
using Amanet.Application.DTOs.Documents;
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
    DateTimeOffset? CompletedAtUtc, IReadOnlyList<DeliveryAttemptDto> DeliveryAttempts);

// Deliberate allowlist: no matter/client navigation, notes, recipient address or storage identifiers.
public sealed record PublicRequestContext(Guid RequestId, ExternalAction Action, string Title,
    RequestProgress Progress, DateTimeOffset ExpiresAtUtc, bool CanSubmit, bool CanReadPdf);

public interface IExternalRequestService
{
    // Create/resend commit before SMTP and must be called outside caller-owned database transactions.
    Task<ExternalRequestDto> CreateAsync(int matterId, CreateExternalRequest input, CancellationToken cancellationToken);
    Task<IReadOnlyList<ExternalRequestDto>> ListAsync(int matterId, CancellationToken cancellationToken);
    Task<ExternalRequestDto> GetAsync(int matterId, Guid requestId, CancellationToken cancellationToken);
    Task<ExternalRequestDto> ResendAsync(int matterId, Guid requestId, EmailTemplate? template, CancellationToken cancellationToken);
    Task<ExternalRequestDto> RevokeAsync(int matterId, Guid requestId, CancellationToken cancellationToken);
    Task<PublicRequestContext> GetContextAsync(Guid requestId, ExternalAction action, string? token, CancellationToken cancellationToken);
    Task<DocumentFile> OpenPdfAsync(Guid requestId, string? token, CancellationToken cancellationToken);

    // Returns a fresh, untracked snapshot so validation never trusts cached link/matter state.
    // Workflow services call this for every public operation and CompleteAsync for completion.
    Task<ExternalRequest> ValidateAsync(Guid requestId, ExternalAction action, string? token,
        bool forSubmission, CancellationToken cancellationToken);
    Task CompleteAsync(Guid requestId, ExternalAction action, string? token, int? signedDocumentVersionId,
        CancellationToken cancellationToken);
}

public interface IEmailSender
{
    // The body is transient. Implementations must never log or persist it or SMTP exception details.
    Task SendAsync(string recipient, string subject, string textBody, CancellationToken cancellationToken);
}
