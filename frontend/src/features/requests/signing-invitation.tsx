"use client";

import Link from "next/link";
import { useRouter } from "next/navigation";
import { BackLink, Feedback, LoadError, PageHeading } from "@/components/workspace-ui";
import { useAdminSession } from "@/features/auth/admin-session";
import { useAdminQuery } from "@/features/auth/use-admin-query";
import {
  documentApiPath, documentHref, isSignableVersion, type DocumentVersion, type PdfDocument,
} from "@/features/documents/document-types";
import type { Matter } from "@/features/matters/matter-types";
import { jsonRequest } from "@/lib/api";
import { RequestForm } from "./request-form";
import { externalRequestsPath, isActiveSigningRequest, requestListHref, type ExternalRequest } from "./request-types";

export function SigningInvitation({ matterId, documentId, versionId }: {
  matterId: number;
  documentId: number;
  versionId: number;
}) {
  const router = useRouter();
  const { request } = useAdminSession();
  const path = documentApiPath(matterId, documentId);
  const requestsPath = externalRequestsPath(matterId);
  const backHref = documentHref(matterId, documentId, true);
  const matter = useAdminQuery<Matter>(`/matters/${matterId}`);
  const record = useAdminQuery<PdfDocument>(path);
  const history = useAdminQuery<DocumentVersion[]>(`${path}/versions`);
  const records = useAdminQuery<ExternalRequest[]>(requestsPath);
  const version = history.data?.find((item) => item.id === versionId);
  const active = records.data?.find((item) =>
    isActiveSigningRequest(item) && history.data?.some((entry) => entry.id === item.sourceVersionId));
  const pending = matter.pending || record.pending || history.pending || records.pending;
  const failed = matter.error || record.error || history.error || records.error;
  const loaded = matter.data && record.data && history.data && records.data;
  const signingDocument = record.data && version && records.data && isSignableVersion(record.data, version, records.data)
    ? { title: record.data.title, originalFileName: version.originalFileName, versionNumber: version.versionNumber }
    : null;

  function refresh() {
    record.refresh();
    matter.refresh();
    history.refresh();
    records.refresh();
  }

  return (
    <>
      <BackLink href={backHref}>Document</BackLink>
      <PageHeading title="Invite external signer" />
      <div className="space-y-5">
        {pending && <p role="status" className="text-sm text-neutral-600">{loaded ? "Refreshing signing status…" : "Loading signing details…"}</p>}
        {failed && <LoadError error={failed} resource="Signing details" onRetry={refresh} />}
        {loaded && matter.data?.archivedAtUtc && <Feedback>This matter is archived. Its requests are read-only.</Feedback>}
        {loaded && !matter.data?.archivedAtUtc && !signingDocument && <Feedback>This PDF version is unavailable for signing.</Feedback>}
        {loaded && !matter.data?.archivedAtUtc && signingDocument && active && (
          <Feedback>
            An external signing request is already active for this document.{" "}
            <Link href={`${requestListHref(matterId)}/${active.id}`} className="admin-link">View signing request</Link>
          </Feedback>
        )}
        {loaded && !matter.data?.archivedAtUtc && signingDocument && !active && (
          <RequestForm signingDocument={signingDocument} refreshing={pending || !!failed}
            submitRequest={(details) => request<ExternalRequest>(requestsPath, jsonRequest("POST", {
              ...details, action: "Sign", documentVersionId: versionId,
            }))}
            onRefresh={refresh}
            onCreated={(created) => router.push(`${requestListHref(matterId)}/${created.id}?created=1`)}
            onCancel={() => router.push(backHref)} />
        )}
      </div>
    </>
  );
}
