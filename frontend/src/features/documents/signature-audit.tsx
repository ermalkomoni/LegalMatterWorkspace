import { useState } from "react";
import { DetailValue, Feedback, LoadError, Timestamp } from "@/components/workspace-ui";
import { useAdminSession } from "@/features/auth/admin-session";
import { ApiError } from "@/lib/api";
import { documentApiPath, type PdfDocument } from "./document-types";
import type { Signature } from "./signing-types";

export function SignatureAudit({ document, signature, pending, error, onRetry }: {
  document: PdfDocument;
  signature?: Signature;
  pending: boolean;
  error?: Error;
  onRetry: () => void;
}) {
  const { requestPdf } = useAdminSession();
  const [downloading, setDownloading] = useState(false);
  const [downloadFailed, setDownloadFailed] = useState(false);

  async function download() {
    if (!signature || downloading) return;
    setDownloading(true);
    setDownloadFailed(false);
    try {
      const path = documentApiPath(document.matterId, document.id);
      const pdf = await requestPdf(`${path}/versions/${signature.finalVersionNumber}/file?download=true`);
      const url = URL.createObjectURL(pdf);
      const link = globalThis.document.createElement("a");
      link.href = url;
      link.download = signature.finalFileName;
      window.setTimeout(() => URL.revokeObjectURL(url), 1000);
      globalThis.document.body.append(link);
      link.click();
      link.remove();
    } catch {
      setDownloadFailed(true);
    } finally {
      setDownloading(false);
    }
  }

  return (
    <section className="admin-panel space-y-4 p-4 sm:p-5" aria-labelledby="document-signature-heading">
      <h2 id="document-signature-heading" tabIndex={-1} className="text-xl font-semibold tracking-tight">Signature</h2>
      {pending && !signature && <p role="status" className="text-sm text-neutral-600">Loading signature…</p>}
      {error instanceof ApiError && error.status === 404
        ? <Feedback>Signer details are unavailable. View the signed PDF in version history.</Feedback>
        : error && <LoadError error={error} resource="signature" compact onRetry={onRetry} />}
      {signature && (
        <>
          <dl className="grid gap-4 sm:grid-cols-3">
            <DetailValue label="Signer">
              {signature.signerName}<br />
              <span className="text-sm text-neutral-700">{signature.signerEmail} · {signature.signerKind === "Admin" ? "Admin" : "External client"}</span>
            </DetailValue>
            <DetailValue label="Signed at"><Timestamp value={signature.signedAtUtc} /></DetailValue>
            <DetailValue label="Final version">Version {signature.finalVersionNumber} · {signature.finalFileName}</DetailValue>
          </dl>
          <button type="button" className="admin-secondary" disabled={downloading} onClick={() => void download()}>
            {downloading ? "Downloading…" : "Download signed PDF"}
          </button>
          {downloadFailed && <Feedback kind="error">Couldn’t download the signed PDF. Try again.</Feedback>}
        </>
      )}
    </section>
  );
}
