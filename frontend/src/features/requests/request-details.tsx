"use client";

import Link from "next/link";
import { useState } from "react";
import { BackLink, DetailValue, Feedback, LoadError, PageHeading, Timestamp } from "@/components/workspace-ui";
import { useAdminSession } from "@/features/auth/admin-session";
import { useAdminQuery } from "@/features/auth/use-admin-query";
import { documentHref } from "@/features/documents/document-types";
import type { Matter } from "@/features/matters/matter-types";
import { ApiError, jsonRequest } from "@/lib/api";
import { RequestDelivery } from "./request-delivery";
import {
  externalRequestsPath, isActiveSigningRequest, isSigningRequest, isUploadRequest, linkStatus, requestListHref,
  uploadStatusLabels, type ExternalRequest,
} from "./request-types";
import { SubmissionHistory } from "./submission-history";

type Area = "delivery" | "submissions";
type ActionFeedback = { area: Area; message: string; kind: "status" | "error" };

function emailResult(request: ExternalRequest) {
  switch (request.deliveryAttempts[0]?.status) {
    case "Sent": return "The latest email was sent.";
    case "Failed": return "The latest email failed. Check the delivery attempt and resend when ready.";
    case "Pending": return "The latest email is still pending.";
    default: return "Email delivery status is unavailable.";
  }
}

export function RequestDetails({ matterId, requestId, created, fromNotifications }: {
  matterId: number;
  requestId: string;
  created: boolean;
  fromNotifications: boolean;
}) {
  const { request } = useAdminSession();
  const path = `${externalRequestsPath(matterId)}/${requestId}` as const;
  const record = useAdminQuery<ExternalRequest>(path);
  const matter = useAdminQuery<Matter>(`/matters/${matterId}`);
  const [busy, setBusy] = useState<Area | null>(null);
  const [feedback, setFeedback] = useState<ActionFeedback | null>(null);
  const item = record.data && (isUploadRequest(record.data) || isSigningRequest(record.data)) ? record.data : null;
  const adminSignature = record.data?.action === "Sign" && record.data.signerKind === "Admin" ? record.data : null;
  const detail = item ?? adminSignature;
  const archived = !!matter.data?.archivedAtUtc;
  const writable = !!matter.data && !archived;
  const availability = item ? linkStatus(item) : null;

  async function mutate(suffix: string, options: RequestInit, area: Area, success: string, reportEmail = false) {
    if (busy || !writable) return false;
    setBusy(area);
    setFeedback(null);
    try {
      const saved = await request<ExternalRequest>(`${path}/${suffix}`, options);
      record.refresh(saved);
      setFeedback({ area, kind: "status", message: reportEmail ? `${success} ${emailResult(saved)}` : success });
      return true;
    } catch (error) {
      const fieldError = error instanceof ApiError ? error.fieldErrors.explanation?.[0] : undefined;
      setFeedback({ area, kind: "error", message: fieldError ?? "Couldn’t confirm the change. Check the refreshed request before trying again." });
      record.refresh();
      matter.refresh();
      return false;
    } finally {
      setBusy(null);
    }
  }

  function actionFeedback(area: Area) {
    if (busy === area) return <p role="status" className="text-sm text-neutral-600">Updating request…</p>;
    if (feedback?.area === area) return <Feedback kind={feedback.kind}>{feedback.message}</Feedback>;
  }

  return (
    <>
      <BackLink href={fromNotifications ? "/notifications" : requestListHref(matterId)}>
        {fromNotifications ? "Notifications" : "Client requests"}
      </BackLink>
      {matter.data && <p className="mb-1 text-sm font-semibold text-neutral-600 wrap-anywhere">{matter.data.referenceNumber} · {matter.data.title}</p>}
      <PageHeading title={detail?.title ?? "Request details"} action={item && (
        <button type="button" className="admin-secondary" disabled={!!busy || record.pending} onClick={() => {
          record.refresh();
          matter.refresh();
        }}>{record.pending && !busy ? "Refreshing…" : "Refresh request"}</button>
      )} />
      <div className="space-y-5">
        {record.pending && !record.data && <p role="status" className="text-sm text-neutral-600">Loading request…</p>}
        {record.error && <LoadError error={record.error} resource="Request" onRetry={record.refresh} />}
        {record.data && !detail && <Feedback kind="error">Request could not be found.</Feedback>}
        {matter.error && <LoadError error={matter.error} resource="Matter" onRetry={matter.refresh} />}
        {archived && <Feedback>This matter is archived. Its requests are read-only.</Feedback>}
        {adminSignature && (
          <section className="admin-panel space-y-4 p-4 sm:p-5" aria-labelledby="admin-signature-heading">
            <h2 id="admin-signature-heading" className="text-xl font-semibold tracking-tight">Admin signature</h2>
            <dl className="grid gap-4 sm:grid-cols-2">
              <DetailValue label="Signer">{adminSignature.recipientName}</DetailValue>
              <DetailValue label="Signed at">{adminSignature.completedAtUtc && <Timestamp value={adminSignature.completedAtUtc} />}</DetailValue>
            </dl>
            {adminSignature.signingDocumentId ? (
              <Link href={documentHref(matterId, adminSignature.signingDocumentId, true)} className="admin-link">View signed document</Link>
            ) : <Link href={`/documents?matterId=${matterId}`} className="admin-link">Matter documents</Link>}
          </section>
        )}
        {item && (
          <>
            {created && !busy && !feedback && <Feedback>Request created. {emailResult(item)}</Feedback>}
            <dl className="admin-panel grid gap-5 p-4 sm:grid-cols-2 sm:p-5 lg:grid-cols-4">
              <DetailValue label={item.action === "Sign" ? "Signing progress" : "Status"}>
                {item.action === "Sign" ? item.progress : uploadStatusLabels[item.uploadStatus]}
              </DetailValue>
              <DetailValue label="Recipient">{item.recipientName && `${item.recipientName}\n`}{item.recipientEmail}</DetailValue>
              <DetailValue label="Link">{availability}</DetailValue>
              <DetailValue label="Expiry"><Timestamp value={item.expiresAtUtc} /></DetailValue>
              {item.action === "Sign" && <>
                {item.viewedAtUtc && <DetailValue label="Viewed at"><Timestamp value={item.viewedAtUtc} /></DetailValue>}
                {item.startedAtUtc && <DetailValue label="Started at"><Timestamp value={item.startedAtUtc} /></DetailValue>}
                {item.completedAtUtc && <DetailValue label="Signed at"><Timestamp value={item.completedAtUtc} /></DetailValue>}
              </>}
              {item.instructions && <DetailValue label="Instructions" className="sm:col-span-2 lg:col-span-4">{item.instructions}</DetailValue>}
            </dl>
            {item.action === "Upload" ? (
              <SubmissionHistory matterId={matterId} submissions={item.submissions} canReview={writable && item.uploadStatus === "Submitted"}
                busy={!!busy || record.pending} feedback={actionFeedback("submissions")}
                onReview={(versionId, decision, explanation) => mutate(`submissions/${versionId}/review`,
                  jsonRequest("POST", { decision, explanation }), "submissions",
                  decision === "Accepted" ? "Submission accepted." : "Corrections requested.", decision === "CorrectionsRequested")} />
            ) : (
              <div className="space-y-2 text-sm">
                {writable && item.progress !== "Signed" && availability !== "Active" && (
                  <Feedback>Create a new invitation from the document to request a signature again.</Feedback>
                )}
                {item.signingDocumentId ? (
                  <Link href={documentHref(matterId, item.signingDocumentId, true)} className="admin-link">View document</Link>
                ) : <Link href={`/documents?matterId=${matterId}`} className="admin-link">Matter documents</Link>}
              </div>
            )}
            <RequestDelivery request={item} busy={!!busy || record.pending} feedback={actionFeedback("delivery")}
              canResend={writable && (item.action === "Sign"
                ? item.progress === "Signed" || isActiveSigningRequest(item)
                : item.uploadStatus === "AwaitingUpload" || item.uploadStatus === "CorrectionsRequested")}
              canRevoke={writable && availability === "Active"}
              onResend={(template) => void mutate("resend", jsonRequest("POST", { template }), "delivery", "New link issued.", true)}
              onRevoke={() => mutate("revoke", { method: "POST" }, "delivery", "Link revoked.")} />
          </>
        )}
      </div>
    </>
  );
}
