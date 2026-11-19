"use client";

import { StatusPill } from "@/components/status-pill";
import { useListSearch } from "@/lib/use-list-search";
import { Icon } from "@/components/icon";
import Link from "next/link";
import { useRouter } from "next/navigation";
import { useRef, useState } from "react";
import { BackLink, Feedback, LoadError, PageHeading, Pagination, Timestamp } from "@/components/workspace-ui";
import { useAdminQuery } from "@/features/auth/use-admin-query";
import type { Matter } from "@/features/matters/matter-types";
import { apiPath } from "@/lib/api";
import type { PagedResult } from "@/lib/pagination";
import { DocumentForm } from "./document-form";
import { documentHref, documentStatus, type PdfDocument } from "./document-types";

export function DocumentLibrary({ matterId }: { matterId: number | null }) {
  const router = useRouter();
  const scope = useAdminQuery<Matter>(matterId ? `/matters/${matterId}` : null);
  const { draft, setDraft, search, page, setPage, clear } = useListSearch();
  const [uploading, setUploading] = useState(false);
  const uploadButton = useRef<HTMLButtonElement>(null);
  const { data, error, pending, refresh } = useAdminQuery<PagedResult<PdfDocument>>(
    !matterId || scope.data ? apiPath("/documents", { matterId: matterId ?? "", search, page }) : null,
  );
  const matter = scope.data;
  const readOnly = !!matter?.archivedAtUtc;

  return (
    <>
      {matterId && <BackLink href={`/matters/${matterId}`}>Matter</BackLink>}
      {matter && <p className="mb-1 text-sm font-semibold text-neutral-600 wrap-anywhere">{matter.referenceNumber} · {matter.title}</p>}
      <PageHeading title={matterId ? "Matter documents" : "PDF library"} description={matterId ? undefined : "Find PDFs across matters and review their latest versions."} action={matter && !readOnly && (
        <button ref={uploadButton} type="button" className="admin-primary" disabled={uploading}
          onClick={() => setUploading(true)}>Upload PDF</button>
      )} />

      <div className="space-y-5">
        {scope.pending && !matter && <p role="status" className="text-sm text-neutral-600">Loading matter…</p>}
        {scope.error && <LoadError error={scope.error} resource="Matter" onRetry={scope.refresh} />}
        {readOnly && <Feedback>This matter is archived. Its documents are read-only.</Feedback>}
        {matterId && uploading && !readOnly && matter && (
          <DocumentForm mode="new" matterId={matterId} onCancel={() => {
            setUploading(false);
            requestAnimationFrame(() => uploadButton.current?.focus());
          }} onConflict={scope.refresh} onSaved={(version) => {
            setUploading(false);
            refresh();
            router.push(`${documentHref(matterId, version.documentId, true)}&uploaded=1`);
          }} />
        )}
        {(!matterId || matter) && (
          <div className="directory-tools">
            <label className="search-box"><Icon name="search" size={17} /><span className="sr-only">Search documents</span>
              <input type="search" maxLength={200} value={draft} placeholder="Search documents…" onChange={(e) => setDraft(e.target.value)} />
            </label>
            {draft && <button type="button" className="text-action" onClick={clear}>Clear search</button>}
            {data && <span className="directory-count">{data.totalCount} {data.totalCount === 1 ? "document" : "documents"}</span>}
          </div>
        )}

        {pending && <p role="status" className="text-sm text-neutral-600">{data ? "Refreshing documents…" : "Loading documents…"}</p>}
        {error && <LoadError error={error} resource="documents" onRetry={refresh} />}
        {data && (
          <>
            {data.items.length ? (
              <ul aria-label="Documents" className="admin-panel divide-y divide-stone-200">
                {data.items.map((item) => {
                  const status = documentStatus(item);
                  return (
                    <li key={item.id} className={`grid gap-2 px-4 py-4 sm:items-start sm:gap-4 sm:px-5 ${matterId
                      ? "sm:grid-cols-[minmax(0,2fr)_minmax(0,1fr)]"
                      : "sm:grid-cols-[minmax(0,2fr)_minmax(0,1fr)_minmax(0,1fr)]"}`}>
                      <div className="min-w-0 space-y-1 wrap-anywhere">
                        <Link className="admin-link inline-flex items-center gap-2" href={documentHref(item.matterId, item.id, !!matterId)}><Icon name="file" />{item.title}</Link>
                        {item.category && <p className="text-sm text-neutral-600">{item.category}</p>}
                        {status && <div><StatusPill value={status} /></div>}
                      </div>
                      {!matterId && <p className="min-w-0 text-sm text-neutral-700 wrap-anywhere">
                        Matter <Link className="admin-link" href={`/matters/${item.matterId}`}>{item.matterReference}</Link>
                      </p>}
                      <div className="min-w-0 space-y-1 text-sm text-neutral-600">
                        <p className="font-medium text-neutral-900">Version {item.latestVersionNumber}</p>
                        <p>Updated <Timestamp value={item.updatedAtUtc} /></p>
                      </div>
                    </li>
                  );
                })}
              </ul>
            ) : (
              <div className="admin-panel p-5">
                <p role="status" className="font-medium">
                  {search ? "No documents match your search." : matterId ? "No PDFs in this matter yet." : "No documents to display."}
                </p>
              </div>
            )}
            <Pagination result={data} onPage={setPage} />
          </>
        )}
      </div>
    </>
  );
}
