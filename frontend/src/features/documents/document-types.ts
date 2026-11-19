import type { ExternalRequest } from "@/features/requests/request-types";

export type PdfDocument = {
  id: number;
  matterId: number;
  matterReference: string;
  title: string;
  category: string | null;
  latestVersionNumber: number;
  updatedAtUtc: string;
  classification: "Standard" | "ReviewPending" | "Accepted";
  signedAtUtc: string | null;
};

export type DocumentVersion = {
  id: number;
  documentId: number;
  versionNumber: number;
  originalFileName: string;
  byteLength: number;
  source: string;
  uploadedAtUtc: string;
};

export function documentApiPath(matterId: number, documentId: number) {
  return `/matters/${matterId}/documents/${documentId}` as const;
}

export function documentHref(matterId: number, documentId: number, fromMatter: boolean) {
  return `/documents/${matterId}/${documentId}${fromMatter ? "?fromMatter=1" : ""}`;
}

export function documentStatus(document: PdfDocument) {
  if (document.signedAtUtc) return "Signed";
  if (document.classification === "ReviewPending") return "Awaiting review";
  if (document.classification === "Accepted") return "Accepted";
  return null;
}

export function isSignableVersion(document: PdfDocument, version: DocumentVersion, requests: ExternalRequest[]) {
  if (document.signedAtUtc || version.documentId !== document.id) return false;
  if (document.classification === "Standard") return version.source === "AdminUpload";
  return document.classification === "Accepted" && version.source === "ClientUpload" && requests.some((request) =>
    request.action === "Upload" && request.uploadStatus === "Accepted" && request.submissions.some((submission) =>
      submission.documentId === document.id && submission.documentVersionId === version.id && submission.decision === "Accepted"));
}
