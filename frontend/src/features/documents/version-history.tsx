import Link from "next/link";
import { useId, useState } from "react";
import { LoadError, Timestamp } from "@/components/workspace-ui";
import { signingRequestHref } from "@/features/requests/request-types";
import { AdminSignatureForm } from "./admin-signature-form";
import { documentApiPath, type DocumentVersion, type PdfDocument } from "./document-types";
import { PdfPreview } from "./pdf-preview";
import type { Signature } from "./signing-types";

const sourceLabels: Record<string, string> = {
  AdminUpload: "Admin upload", ClientUpload: "Client upload", TypedSignature: "Signed PDF",
};

const sizeFormatter = new Intl.NumberFormat("en", { maximumFractionDigits: 1 });

function fileSize(bytes: number) {
  if (bytes < 1024) return `${bytes} B`;
  if (bytes < 1024 * 1024) return `${sizeFormatter.format(bytes / 1024)} KiB`;
  return `${sizeFormatter.format(bytes / (1024 * 1024))} MiB`;
}

export function VersionHistory({ document, versions, pending, error, onRetry, signing }: {
  document: PdfDocument;
  versions?: DocumentVersion[];
  pending: boolean;
  error?: Error;
  onRetry: () => void;
  signing: {
    selectedVersionId: number | null;
    canSign: (version: DocumentVersion) => boolean;
    canStart: boolean;
    onStart: (versionId: number) => void;
    onCancel: () => void;
    onSigned: (signature: Signature) => void;
    onRefresh: () => void;
  };
}) {
  const sectionId = useId();
  const [selection, setSelection] = useState<number | null>(null);
  const [savingSignature, setSavingSignature] = useState(false);
  const path = documentApiPath(document.matterId, document.id);

  return (
    <section className="admin-panel" aria-labelledby={`${sectionId}-heading`}>
      <div className="space-y-2 p-4 sm:px-6 sm:py-5">
        <h2 id={`${sectionId}-heading`} className="text-xl font-semibold tracking-tight">Version history</h2>
        {document.classification !== "Standard" && !document.signedAtUtc && (
          <p className="text-sm text-neutral-600">New versions of client-submitted documents come through their request.</p>
        )}
        {pending && <p role="status" className="text-sm text-neutral-600">{versions ? "Refreshing versions…" : "Loading versions…"}</p>}
        {error && <LoadError error={error} resource="version history" compact onRetry={onRetry} />}
        {versions?.length === 0 && <p className="text-sm text-neutral-600">No versions to display.</p>}
      </div>
      {!!versions?.length && (
        <ol className="divide-y divide-stone-200 border-t border-stone-200">
          {versions.map((version) => {
            const signingThisVersion = signing.selectedVersionId === version.id;
            const selected = signingThisVersion || (signing.selectedVersionId === null && selection === version.id);
            const previewId = `${sectionId}-version-${version.id}`;
            const signButtonId = `${previewId}-sign`;
            const canSign = signing.canSign(version);
            return (
              <li key={version.id} className="space-y-3 px-4 py-4 sm:px-6">
                <div className="flex flex-wrap items-start justify-between gap-3">
                  <div className="min-w-0 space-y-1 wrap-anywhere">
                    <h3 className="font-semibold">Version {version.versionNumber}</h3>
                    <p className="text-sm text-neutral-800">{version.originalFileName}</p>
                    <p className="text-sm text-neutral-600">
                      <Timestamp value={version.uploadedAtUtc} /> · {fileSize(version.byteLength)}
                      {sourceLabels[version.source] && <> · {sourceLabels[version.source]}</>}
                    </p>
                  </div>
                  <div className="flex flex-wrap gap-2">
                    {!signingThisVersion && (
                      <button id={`${previewId}-toggle`} type="button" className="admin-secondary" aria-expanded={selected} aria-controls={previewId}
                        aria-label={selected ? `Close preview for version ${version.versionNumber}` : `View PDF for version ${version.versionNumber}`}
                        disabled={signing.selectedVersionId !== null} onClick={() => setSelection(selected ? null : version.id)}>
                        {selected ? "Close preview" : "View PDF"}
                      </button>
                    )}
                    {canSign && signing.selectedVersionId === null && (
                      <>
                        <button id={signButtonId} type="button" className="admin-secondary" aria-controls={previewId}
                          disabled={!signing.canStart} onClick={() => {
                            signing.onStart(version.id);
                            requestAnimationFrame(() => globalThis.document.getElementById(`${previewId}-review`)?.focus());
                          }}>Sign as Admin</button>
                        {signing.canStart && <Link className="admin-secondary"
                          href={signingRequestHref(document.matterId, document.id, version.id)}>Invite external signer</Link>}
                      </>
                    )}
                    {signingThisVersion && (
                      <button type="button" className="admin-secondary" disabled={savingSignature} onClick={() => {
                        signing.onCancel();
                        requestAnimationFrame(() => globalThis.document.getElementById(canSign ? signButtonId : `${previewId}-toggle`)?.focus());
                      }}>Cancel signing</button>
                    )}
                  </div>
                </div>
                <div id={previewId} hidden={!selected} className="space-y-3 rounded-lg bg-stone-50 p-3 sm:p-4">
                  {signingThisVersion && <h4 id={`${previewId}-review`} tabIndex={-1} className="font-semibold">Review version {version.versionNumber}</h4>}
                  {selected && <PdfPreview path={`${path}/versions/${version.versionNumber}/file`} version={version}>
                    {signingThisVersion && <AdminSignatureForm document={document} version={version} canSubmit={canSign}
                      saving={savingSignature} onSaving={setSavingSignature} onSigned={signing.onSigned} onRefresh={signing.onRefresh} />}
                  </PdfPreview>}
                </div>
              </li>
            );
          })}
        </ol>
      )}
    </section>
  );
}
