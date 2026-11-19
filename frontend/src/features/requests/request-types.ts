export type UploadStatus = "AwaitingUpload" | "Submitted" | "CorrectionsRequested" | "Accepted";
export type SubmissionDecision = "Accepted" | "CorrectionsRequested";
export type EmailTemplate = "DocumentRequest" | "SignatureInvitation" | "Correction" | "Reminder";

export type DeliveryAttempt = {
  id: string;
  template: EmailTemplate;
  status: "Pending" | "Sent" | "Failed";
  startedAtUtc: string;
  finishedAtUtc: string | null;
  failureCode: string | null;
};

export type DocumentSubmission = {
  documentVersionId: number;
  documentId: number;
  versionNumber: number;
  fileName: string;
  submittedAtUtc: string;
  decision: SubmissionDecision | null;
  correctionExplanation: string | null;
  reviewedAtUtc: string | null;
};

export type ExternalRequest = {
  id: string;
  action: "Upload" | "Sign";
  title: string;
  recipientName: string;
  recipientEmail: string;
  instructions: string;
  expiresAtUtc: string;
  revokedAtUtc: string | null;
  uploadStatus: UploadStatus | null;
  deliveryAttempts: DeliveryAttempt[];
  submissions: DocumentSubmission[];
};

export type UploadRequest = ExternalRequest & { action: "Upload"; uploadStatus: UploadStatus };

export type PublicUploadContext = {
  title: string;
  recipientName: string;
  instructions: string;
  correctionExplanation: string | null;
  expiresAtUtc: string;
  canSubmit: boolean;
  uploadStatus: UploadStatus;
};

export const uploadStatusLabels = {
  AwaitingUpload: "Awaiting upload",
  Submitted: "Awaiting review",
  CorrectionsRequested: "Corrections requested",
  Accepted: "Accepted",
} satisfies Record<UploadStatus, string>;

export const templateLabels = {
  DocumentRequest: "Invitation", SignatureInvitation: "Signature invitation", Correction: "Correction", Reminder: "Reminder",
} satisfies Record<EmailTemplate, string>;

export function externalRequestsPath(matterId: number) {
  return `/matters/${matterId}/external-requests` as const;
}

export function requestListHref(matterId: number) {
  return `/matters/${matterId}/requests` as const;
}

export function isRequestId(value: string) {
  return /^[a-f0-9]{8}-(?:[a-f0-9]{4}-){3}[a-f0-9]{12}$/i.test(value);
}

export function isUploadRequest(request: ExternalRequest): request is UploadRequest {
  return request.action === "Upload" && request.uploadStatus !== null;
}

export function linkStatus(request: ExternalRequest) {
  if (request.revokedAtUtc) return "Revoked";
  return Date.parse(request.expiresAtUtc) <= Date.now() ? "Expired" : "Active";
}
