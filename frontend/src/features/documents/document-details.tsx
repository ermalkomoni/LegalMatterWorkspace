"use client";

import Link from "next/link";
import { useRef, useState } from "react";
import { BackLink, DetailValue, Feedback, LoadError, PageHeading, Timestamp } from "@/components/workspace-ui";
import { useAdminQuery } from "@/features/auth/use-admin-query";
import type { Matter } from "@/features/matters/matter-types";
import { DocumentForm } from "./document-form";
import { documentApiPath, documentStatus, type DocumentVersion, type PdfDocument } from "./document-types";
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
  const [mode, setMode] = useState<"metadata" | "version" | null>(null);
  const [notice, setNotice] = useState(uploaded ? "PDF uploaded as version 1." : "");
  const editButton = useRef<HTMLButtonElement>(null);
  const versionButton = useRef<HTMLButtonElement>(null);
  const document = record.data;
  const archived = !!matter.data?.archivedAtUtc;
  const canEdit = !!matter.data && !archived && !document?.signedAtUtc;
  const canUpload = canEdit && document?.classification === "Standard";
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
              {status && <DetailValue label="Status">{status}{document.signedAtUtc && <> · <Timestamp value={document.signedAtUtc} /></>}</DetailValue>}
            </dl>
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
            <VersionHistory document={document} versions={history.data} pending={history.pending} error={history.error} onRetry={history.refresh} />
          </>
        )}
      </div>
    </>
  );
}
