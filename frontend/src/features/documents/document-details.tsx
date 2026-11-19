"use client";

import Link from "next/link";
import { useRef, useState } from "react";
import { BackLink, DetailValue, Feedback, LoadError, PageHeading } from "@/components/workspace-ui";
import { useAdminQuery } from "@/features/auth/use-admin-query";
import type { Matter } from "@/features/matters/matter-types";
import { externalRequestsPath, isActiveSigningRequest, requestListHref, type ExternalRequest } from "@/features/requests/request-types";
import { DocumentForm } from "./document-form";
import { documentApiPath, documentStatus, isSignableVersion, type DocumentVersion, type PdfDocument } from "./document-types";
import { SignatureAudit } from "./signature-audit";
import { signatureApiPath, type Signature } from "./signing-types";
import { VersionHistory } from "./version-history";

export function DocumentDetails({ matterId, documentId, fromMatter, uploaded }: {
  matterId: number;
  documentId: number;
  fromMatter: boolean;
  uploaded: boolean;
}) {
  const path = documentApiPath(matterId, documentId);
  const record = useAdminQuery<PdfDocument>(path);
  const matter = useAdminQuery<Matter>(record.data ? `/matters/${matterId}` : null);
  const history = useAdminQuery<DocumentVersion[]>(record.data ? `${path}/versions` : null);
  const [mode, setMode] = useState<"metadata" | "version" | number | null>(null);
  const [notice, setNotice] = useState(uploaded ? "PDF uploaded as version 1." : "");
  const editButton = useRef<HTMLButtonElement>(null);
  const versionButton = useRef<HTMLButtonElement>(null);
  const document = record.data;
  const archived = !!matter.data?.archivedAtUtc;
  const canEdit = !!document && !!matter.data && !archived && !document.signedAtUtc;
  const requests = useAdminQuery<ExternalRequest[]>(canEdit && document.classification !== "ReviewPending" ? externalRequestsPath(matterId) : null);
  const signature = useAdminQuery<Signature>(document?.signedAtUtc ? signatureApiPath(matterId, documentId) : null);
  const activeInvitation = requests.data?.find((request) => isActiveSigningRequest(request) &&
    history.data?.some((version) => version.id === request.sourceVersionId));
  const canUpload = canEdit && !activeInvitation && document.classification === "Standard";
  const status = document ? documentStatus(document) : null;

  function closeForm() {
    const button = mode === "metadata" ? editButton : versionButton;
    setMode(null);
    requestAnimationFrame(() => button.current?.focus());
  }

  function refreshDetails() {
    record.refresh();
    matter.refresh();
    history.refresh();
    requests.refresh();
  }

  function showSignature(saved: Signature) {
    if (!document) return;
    setMode(null);
    setNotice(`PDF signed as version ${saved.finalVersionNumber}.`);
    record.refresh({ ...document, latestVersionNumber: saved.finalVersionNumber, signedAtUtc: saved.signedAtUtc });
    history.refresh();
    requestAnimationFrame(() => globalThis.document.getElementById("document-signature-heading")?.focus());
  }

  return (
    <>
      <BackLink href={fromMatter ? `/documents?matterId=${matterId}` : "/documents"}>
        {fromMatter ? "Matter documents" : "PDF library"}
      </BackLink>
      <PageHeading title={document?.title ?? "Document"} action={document && canEdit && (
        <div className="flex flex-wrap gap-2">
          <button ref={editButton} type="button" className="admin-secondary" disabled={mode !== null}
            onClick={() => { setNotice(""); setMode("metadata"); }}>Edit details</button>
          {canUpload && <button ref={versionButton} type="button" className="admin-primary" disabled={mode !== null}
            onClick={() => { setNotice(""); setMode("version"); }}>Upload new version</button>}
        </div>
      )} />

      <div className="space-y-5">
        {record.pending && <p role="status" className="text-sm text-neutral-600">{document ? "Refreshing document…" : "Loading document…"}</p>}
        {record.error && <LoadError error={record.error} resource="Document" onRetry={record.refresh} />}
        {matter.error && <LoadError error={matter.error} resource="Matter" onRetry={matter.refresh} />}
        {notice && document && <Feedback>{notice}</Feedback>}
        {archived && <Feedback>This matter is archived. Its documents are read-only.</Feedback>}
        {document?.signedAtUtc && <Feedback>This document is signed and read-only.</Feedback>}
        {document && (
          <>
            <dl className="admin-panel grid gap-4 p-4 sm:grid-cols-4 sm:p-5">
              <DetailValue label="Matter"><Link href={`/matters/${matterId}`} className="admin-link">{document.matterReference}</Link></DetailValue>
              <DetailValue label="Category">{document.category ?? "No category"}</DetailValue>
              <DetailValue label="Latest version">{document.latestVersionNumber}</DetailValue>
              {status && <DetailValue label="Status">{status}</DetailValue>}
            </dl>
            {document.signedAtUtc && <SignatureAudit document={document} signature={signature.data} pending={signature.pending}
              error={signature.error} onRetry={signature.refresh} />}
            {mode === "metadata" && canEdit && (
              <DocumentForm mode="metadata" document={document} onCancel={closeForm} onConflict={refreshDetails}
                onSaved={(saved) => {
                  record.refresh(saved);
                  setNotice("Document details saved.");
                  closeForm();
                }} />
            )}
            {mode === "version" && canUpload && (
              <DocumentForm mode="version" document={document} onCancel={closeForm} onConflict={refreshDetails}
                onSaved={(version) => {
                  record.refresh({ ...document, latestVersionNumber: version.versionNumber, updatedAtUtc: version.uploadedAtUtc });
                  history.refresh(history.data && [version, ...history.data.filter((item) => item.id !== version.id)]);
                  setNotice(`Version ${version.versionNumber} uploaded.`);
                  closeForm();
                }} />
            )}
            {requests.pending && !requests.data && (
              <p role="status" className="text-sm text-neutral-600">Checking signing availability…</p>
            )}
            {requests.error && <LoadError error={requests.error} resource="signing availability" onRetry={requests.refresh} />}
            {activeInvitation && <div className="flex flex-wrap items-center gap-x-3 gap-y-1 text-sm">
              <p>An external signing invitation is active.</p>
              <Link href={`${requestListHref(matterId)}/${activeInvitation.id}`} className="admin-link">View signing invitation</Link>
            </div>}
            <VersionHistory document={document} versions={history.data} pending={history.pending} error={history.error} onRetry={history.refresh}
              signing={{
                selectedVersionId: typeof mode === "number" && canEdit ? mode : null,
                canSign: (version) => canEdit && !activeInvitation && !requests.pending && !requests.error &&
                  !!requests.data && isSignableVersion(document, version, requests.data),
                canStart: mode === null,
                onStart: (versionId) => { setNotice(""); setMode(versionId); },
                onCancel: () => setMode(null),
                onSigned: showSignature,
                onRefresh: refreshDetails,
              }} />
          </>
        )}
      </div>
    </>
  );
}
