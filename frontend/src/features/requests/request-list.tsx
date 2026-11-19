"use client";

import Link from "next/link";
import { useRouter } from "next/navigation";
import { useRef, useState } from "react";
import { BackLink, Feedback, LoadError, PageHeading, Timestamp } from "@/components/workspace-ui";
import { useAdminSession } from "@/features/auth/admin-session";
import { useAdminQuery } from "@/features/auth/use-admin-query";
import type { Matter } from "@/features/matters/matter-types";
import { jsonRequest } from "@/lib/api";
import { RequestForm } from "./request-form";
import { externalRequestsPath, isUploadRequest, linkStatus, requestListHref, uploadStatusLabels, type ExternalRequest } from "./request-types";

export function RequestList({ matterId }: { matterId: number }) {
  const router = useRouter();
  const { request } = useAdminSession();
  const matter = useAdminQuery<Matter>(`/matters/${matterId}`);
  const path = externalRequestsPath(matterId);
  const records = useAdminQuery<ExternalRequest[]>(matter.data ? path : null);
  const [creating, setCreating] = useState(false);
  const createButton = useRef<HTMLButtonElement>(null);
  const archived = !!matter.data?.archivedAtUtc;
  const uploads = records.data?.filter(isUploadRequest);

  return (
    <>
      <BackLink href={`/matters/${matterId}`}>Matter</BackLink>
      {matter.data && <p className="mb-1 text-sm font-semibold text-neutral-600 wrap-anywhere">{matter.data.referenceNumber} · {matter.data.title}</p>}
      <PageHeading title="Client document requests" action={matter.data && !archived && (
        <button ref={createButton} type="button" className="admin-primary" disabled={creating}
          onClick={() => setCreating(true)}>New request</button>
      )} />
      <div className="space-y-5">
        {matter.pending && !matter.data && <p role="status" className="text-sm text-neutral-600">Loading matter…</p>}
        {matter.error && <LoadError error={matter.error} resource="Matter" onRetry={matter.refresh} />}
        {archived && <Feedback>This matter is archived. Its requests are read-only.</Feedback>}
        {creating && !archived && (
          <RequestForm submitRequest={(details) => request<ExternalRequest>(path, jsonRequest("POST", { action: "Upload", ...details }))}
            onRefresh={() => { records.refresh(); matter.refresh(); }}
            onCreated={(created) => {
              setCreating(false);
              router.push(`${requestListHref(matterId)}/${created.id}?created=1`);
            }}
            onCancel={() => { setCreating(false); requestAnimationFrame(() => createButton.current?.focus()); }} />
        )}
        {records.pending && <p role="status" className="text-sm text-neutral-600">Loading document requests…</p>}
        {records.error && <LoadError error={records.error} resource="document requests" onRetry={records.refresh} />}
        {uploads?.length === 0 && <p className="admin-panel p-5 text-sm text-neutral-700">No client document requests yet.</p>}
        {!!uploads?.length && (
          <ul aria-label="Client document requests" className="admin-panel divide-y divide-stone-200">
            {uploads.map((item) => (
              <li key={item.id} className="grid gap-2 px-4 py-4 sm:grid-cols-[minmax(0,2fr)_minmax(0,1fr)_minmax(0,1fr)] sm:gap-4 sm:px-5">
                <div className="min-w-0 wrap-anywhere">
                  <Link href={`${requestListHref(matterId)}/${item.id}`} className="admin-link">{item.title}</Link>
                  <p className="mt-1 text-sm text-neutral-700">{item.recipientName} · {item.recipientEmail}</p>
                </div>
                <div className="space-y-1 text-sm">
                  <p className="font-semibold">{uploadStatusLabels[item.uploadStatus]}</p>
                  <p className="text-neutral-600">Latest email · {item.deliveryAttempts[0]?.status ?? "No attempt"}</p>
                </div>
                <div className="space-y-1 text-sm text-neutral-600">
                  <p>Link · {linkStatus(item)}</p>
                  <p>Expiry <Timestamp value={item.expiresAtUtc} /></p>
                </div>
              </li>
            ))}
          </ul>
        )}
      </div>
    </>
  );
}
