"use client";

import Link from "next/link";
import { useRouter } from "next/navigation";
import { useState } from "react";
import { BackLink, DetailValue, Feedback, LoadError, PageHeading } from "@/components/workspace-ui";
import { useAdminSession } from "@/features/auth/admin-session";
import { useAdminQuery } from "@/features/auth/use-admin-query";
import { MatterEditor } from "./matter-editor";
import { formatCalendarDate, matterPriorities, matterStatuses, type Matter } from "./matter-types";

export function NewMatter() {
  const router = useRouter();
  return (
    <>
      <BackLink href="/">Matters</BackLink>
      <PageHeading title="New matter" />
      <MatterEditor onSaved={(matter) => router.replace(`/matters/${matter.id}?created=1`)}
        onCancel={() => router.replace("/")} />
    </>
  );
}

export function MatterDetails({ id, created }: { id: number; created: boolean }) {
  const { request } = useAdminSession();
  const { data, error, pending, refresh } = useAdminQuery<Matter>(`/matters/${id}`);
  const [editing, setEditing] = useState(false);
  const [confirmingArchive, setConfirmingArchive] = useState(false);
  const [changing, setChanging] = useState(false);
  const [notice, setNotice] = useState(created ? "Matter created." : "");
  const [actionError, setActionError] = useState("");

  async function changeArchive(action: "archive" | "restore") {
    setChanging(true);
    setActionError("");
    setNotice("");
    try {
      const saved = await request<Matter>(`/matters/${id}/${action}`, { method: "POST" });
      refresh(saved);
      setConfirmingArchive(false);
      setNotice(action === "archive" ? "Matter archived." : "Matter restored.");
    } catch {
      setActionError(action === "archive" ? "Couldn’t archive the matter. Try again." : "Couldn’t restore the matter. Try again.");
    } finally {
      setChanging(false);
    }
  }

  return (
    <>
      <BackLink href="/">Matters</BackLink>
      {data && !editing && <p className="mb-1 text-sm font-semibold text-neutral-600 wrap-anywhere">{data.referenceNumber}</p>}
      <PageHeading title={editing ? "Edit matter" : data?.title ?? "Matter details"}
        action={data && !editing && (
          <div className="flex flex-wrap gap-2">
            {data.archivedAtUtc ? (
              <button type="button" className="admin-secondary" disabled={changing}
                onClick={() => void changeArchive("restore")}>{changing ? "Restoring…" : "Restore matter"}</button>
            ) : (
              <>
                <button type="button" className="admin-secondary" disabled={confirmingArchive}
                  onClick={() => setEditing(true)}>Edit matter</button>
                <button type="button" className="admin-secondary text-red-800" disabled={confirmingArchive}
                  onClick={() => { setActionError(""); setConfirmingArchive(true); }}>Archive matter</button>
              </>
            )}
          </div>
        )} />

      <div className="space-y-4">
        {notice && !editing && <Feedback>{notice}</Feedback>}
        {actionError && <Feedback kind="error">{actionError}</Feedback>}
        {pending && <p role="status" className="text-sm text-neutral-600">{data ? "Refreshing matter…" : "Loading matter…"}</p>}
        {error && <LoadError error={error} resource="Matter" onRetry={refresh} />}
        {data && confirmingArchive && !data.archivedAtUtc && (
          <section aria-labelledby="archive-title" className="admin-panel space-y-3 p-5">
            <h2 id="archive-title" className="font-semibold">Archive this matter?</h2>
            <p className="text-sm text-neutral-700">Archiving makes it read-only and revokes any outstanding external links.</p>
            <div className="flex flex-wrap gap-3">
              <button type="button" className="admin-primary" disabled={changing} onClick={() => void changeArchive("archive")}>
                {changing ? "Archiving…" : "Confirm archive"}
              </button>
              <button type="button" className="admin-secondary" disabled={changing}
                onClick={() => { setConfirmingArchive(false); setActionError(""); }}>Cancel archive</button>
            </div>
          </section>
        )}
        {data && (editing && !data.archivedAtUtc ? (
          <MatterEditor matter={data} onCancel={() => setEditing(false)} onSaved={(saved) => {
            refresh(saved);
            setNotice("Matter updated.");
            setEditing(false);
          }} onArchived={(archived) => {
            refresh(archived);
            setNotice("This matter was archived while you were editing.");
            setEditing(false);
          }} />
        ) : (
          <MatterSummary matter={data} />
        ))}
      </div>
    </>
  );
}

function MatterSummary({ matter }: { matter: Matter }) {
  return (
    <>
      {matter.archivedAtUtc && <Feedback>Archived on {formatCalendarDate(matter.archivedAtUtc.slice(0, 10))}. This matter is read-only.</Feedback>}
      <dl className="admin-panel grid gap-6 p-5 sm:grid-cols-2 sm:p-6 lg:grid-cols-3">
        <DetailValue label="Primary client">
          <Link href={`/clients/${matter.primaryClientId}`} className="admin-link">{matter.primaryClientName}</Link>
        </DetailValue>
        <DetailValue label="Status">{matterStatuses[matter.status]}</DetailValue>
        <DetailValue label="Priority">{matterPriorities[matter.priority]}</DetailValue>
        <DetailValue label="Opened date">{formatCalendarDate(matter.openedDate)}</DetailValue>
        <DetailValue label="Target close date">{matter.targetCloseDate ? formatCalendarDate(matter.targetCloseDate) : null}</DetailValue>
        <DetailValue label="Description" className="sm:col-span-2 lg:col-span-3">{matter.description}</DetailValue>
      </dl>
    </>
  );
}
