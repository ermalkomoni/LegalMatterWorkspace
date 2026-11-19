import { documentApiPath } from "./document-types";

// Keep this wording in sync with SigningConsent.Text in the API contract.
export const signingConsentText = "I have reviewed this document and agree to sign it electronically using my typed name.";

export type Signature = {
  sourceVersionId: number;
  finalVersionId: number;
  finalVersionNumber: number;
  finalFileName: string;
  signerKind: "Admin" | "ExternalClient";
  signerName: string;
  signerEmail: string;
  signedAtUtc: string;
  consentText: string;
};

export function signatureApiPath(matterId: number, documentId: number) {
  return `${documentApiPath(matterId, documentId)}/signature` as const;
}
