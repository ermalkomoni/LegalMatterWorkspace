import { useId, useState } from "react";
import { LoadError, Timestamp } from "@/components/workspace-ui";
import { documentApiPath, type DocumentVersion, type PdfDocument } from "./document-types";
import { PdfPreview } from "./pdf-preview";

const sourceLabels: Record<string, string> = {
  AdminUpload: "Admin upload", ClientUpload: "Client upload", TypedSignature: "Signed PDF",
};

const sizeFormatter = new Intl.NumberFormat("en", { maximumFractionDigits: 1 });

function fileSize(bytes: number) {
  if (bytes < 1024) return `${bytes} B`;
  if (bytes < 1024 * 1024) return `${sizeFormatter.format(bytes / 1024)} KiB`;
  return `${sizeFormatter.format(bytes / (1024 * 1024))} MiB`;
}

export function VersionHistory({ document, versions, pending, error, onRetry }: {
  document: PdfDocument;
  versions?: DocumentVersion[];
  pending: boolean;
  error?: Error;
  onRetry: () => void;
}) {
  const sectionId = useId();
  const [selection, setSelection] = useState<number | null>(null);
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
            const selected = selection === version.id;
            const previewId = `${sectionId}-version-${version.id}`;
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
                  <button type="button" className="admin-secondary" aria-expanded={selected} aria-controls={previewId}
                    aria-label={selected ? `Close preview for version ${version.versionNumber}` : `View PDF for version ${version.versionNumber}`}
                    onClick={() => setSelection(selected ? null : version.id)}>
                    {selected ? "Close preview" : "View PDF"}
                  </button>
                </div>
                <div id={previewId} hidden={!selected} className="rounded-lg bg-stone-50 p-3 sm:p-4">
                  {selected && <PdfPreview path={`${path}/versions/${version.versionNumber}/file`} version={version} />}
                </div>
              </li>
            );
          })}
        </ol>
      )}
    </section>
  );
}
