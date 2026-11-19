import { useEffect, useState } from "react";
import { LoadError } from "@/components/workspace-ui";
import { useAdminSession } from "@/features/auth/admin-session";
import type { ApiPath } from "@/lib/api";
import type { DocumentVersion } from "./document-types";

type FileState = { url: string } | { error: unknown } | null;

export function PdfPreview({ path, version }: {
  path: ApiPath;
  version: Pick<DocumentVersion, "versionNumber" | "originalFileName">;
}) {
  const { requestPdf } = useAdminSession();
  const [file, setFile] = useState<FileState>(null);
  const [attempt, setAttempt] = useState(0);
  const [inlineUnavailable, setInlineUnavailable] = useState(() => !navigator.pdfViewerEnabled);

  useEffect(() => {
    const controller = new AbortController();
    const { signal } = controller;
    let objectUrl: string | undefined;

    void requestPdf(path, { signal }).then((blob) => {
      if (signal.aborted) return;
      objectUrl = URL.createObjectURL(blob);
      setFile({ url: objectUrl });
    }).catch((error: unknown) => {
      if (!signal.aborted) setFile({ error });
    });

    return () => {
      controller.abort();
      if (objectUrl) URL.revokeObjectURL(objectUrl);
    };
  }, [attempt, path, requestPdf]);

  if (!file) return <p role="status" className="text-sm text-neutral-600">Loading PDF…</p>;
  if ("error" in file) {
    return <LoadError error={file.error} resource="PDF file" compact onRetry={() => {
      setFile(null);
      setAttempt((previous) => previous + 1);
    }} />;
  }

  return (
    <div className="space-y-3">
      {inlineUnavailable && <p className="text-sm text-neutral-700">Inline preview is unavailable. Download the PDF to open it.</p>}
      <a className="admin-secondary" href={file.url} download={version.originalFileName}>Download PDF</a>
      {!inlineUnavailable && (
        <iframe title={`PDF preview, version ${version.versionNumber}`} src={file.url}
          className="block h-[65vh] min-h-72 w-full rounded-lg border border-stone-200 bg-white"
          onError={() => setInlineUnavailable(true)} />
      )}
    </div>
  );
}
