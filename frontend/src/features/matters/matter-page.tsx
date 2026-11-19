"use client";

import { StatusPill } from "@/components/status-pill";

import Link from "next/link";
import { useRouter } from "next/navigation";
import { useEffect, useState } from "react";
import { BackLink, DetailValue, Feedback, LoadError, PageHeading } from "@/components/workspace-ui";
import { useAdminSession } from "@/features/auth/admin-session";
import { useAdminQuery } from "@/features/auth/use-admin-query";
import { apiPath } from "@/lib/api";
import type { PagedResult } from "@/lib/pagination";
import { MatterEditor } from "./matter-editor";
import { MatterHistory, type MatterActivity } from "./matter-history";
import { MatterItems, matterCollections, type MatterCollection } from "./matter-items";
import { formatCalendarDate, matterPriorities, matterStatuses, type Matter } from "./matter-types";

const collections = Object.keys(matterCollections) as MatterCollection[];

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

export function MatterDetails({ id, created, notificationsHref, target }: {
  id: number;
  created: boolean;
  notificationsHref: string | null;
  target?: { collection: "tasks" | "deadlines"; id: number };
}) {
  const { request } = useAdminSession();
  const { data, error, pending, refresh } = useAdminQuery<Matter>(`/matters/${id}`);
  const initialSection = target?.collection ?? "tasks";
  const [section, setSection] = useState<MatterCollection | "history">(initialSection);
  useEffect(() => {
    function readSection() {
      const fragment = window.location.hash.slice(1);
      setSection(fragment === "history" || collections.includes(fragment as MatterCollection)
        ? fragment as MatterCollection | "history" : initialSection);
    }
    readSection();
    window.addEventListener("hashchange", readSection);
    return () => window.removeEventListener("hashchange", readSection);
  }, [initialSection]);
  const [activityPage, setActivityPage] = useState(1);
  const activity = useAdminQuery<PagedResult<MatterActivity>>(
    data ? apiPath(`/matters/${id}/activities`, { page: activityPage, pageSize: 20 }) : null,
  );
  const [editing, setEditing] = useState(false);
  const [confirmingArchive, setConfirmingArchive] = useState(false);
  const [changing, setChanging] = useState(false);
  const [notice, setNotice] = useState(created ? "Matter created." : "");
  const [actionError, setActionError] = useState("");

  function refreshActivity() {
    setActivityPage(1);
    activity.refresh();
  }

  async function changeArchive(action: "archive" | "restore") {
    setChanging(true);
    setActionError("");
    setNotice("");
    try {
      const saved = await request<Matter>(`/matters/${id}/${action}`, { method: "POST" });
      refresh(saved);
      refreshActivity();
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
      {notificationsHref ? <BackLink href={notificationsHref}>Notifications</BackLink> : <BackLink href="/">Matters</BackLink>}
      {data && !editing && <p className="mb-1 text-sm font-semibold text-neutral-600 wrap-anywhere">{data.referenceNumber}</p>}
      <PageHeading title={editing ? "Edit matter" : data?.title ?? "Matter details"}
        action={data && !editing && (
          <div className="flex flex-wrap gap-2">
            <Link href={`/documents?matterId=${id}`} className="admin-secondary">Documents</Link>
            <Link href={`/matters/${id}/requests`} className="admin-secondary">Client requests</Link>
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
            refreshActivity();
            setNotice("Matter updated.");
            setEditing(false);
          }} onArchived={(archived) => {
            refresh(archived);
            refreshActivity();
            setNotice("This matter was archived while you were editing.");
            setEditing(false);
          }} />
        ) : (
          <div className="matter-detail">
            <MatterSummary matter={data} />
            <div className="min-w-0">
              <nav aria-label="Matter sections" className="section-nav">
                {collections.map((collection) => (
                  <a key={collection} href={`#${collection}`} aria-current={section === collection ? "location" : undefined}>
                    {matterCollections[collection].title}
                  </a>
                ))}
                <a href="#history" aria-current={section === "history" ? "location" : undefined}>Matter history</a>
              </nav>
              {collections.map((collection) => (
                <div key={collection} hidden={section !== collection}>
                  <MatterItems key={String(!!data.archivedAtUtc)} matterId={id} collection={collection}
                    targetId={target?.collection === collection ? target.id : undefined}
                    readOnly={!!data.archivedAtUtc} onChange={refreshActivity} onConflict={() => {
                      refresh();
                      refreshActivity();
                    }} />
                </div>
              ))}
              <div hidden={section !== "history"}>
                <MatterHistory data={activity.data} pending={activity.pending} error={activity.error} onRetry={activity.refresh}
                  onPage={(page) => {
                    document.getElementById("history")?.focus();
                    setActivityPage(page);
                  }} />
              </div>
            </div>
          </div>
        ))}
      </div>
    </>
  );
}

function MatterSummary({ matter }: { matter: Matter }) {
  return (
    <aside className="case-summary admin-panel">
      <h2>Matter overview</h2>
      {matter.archivedAtUtc && <Feedback>Archived on {formatCalendarDate(matter.archivedAtUtc.slice(0, 10))}. This matter is read-only.</Feedback>}
      <dl className="summary-grid">
        <DetailValue label="Primary client">
          <Link href={`/clients/${matter.primaryClientId}`} className="admin-link">{matter.primaryClientName}</Link>
        </DetailValue>
        <DetailValue label="Status"><StatusPill value={matterStatuses[matter.status]} /></DetailValue>
        <DetailValue label="Priority"><StatusPill value={matterPriorities[matter.priority]} /></DetailValue>
        <DetailValue label="Opened date">{formatCalendarDate(matter.openedDate)}</DetailValue>
        <DetailValue label="Target close date">{matter.targetCloseDate ? formatCalendarDate(matter.targetCloseDate) : null}</DetailValue>
        <DetailValue label="Description" className="summary-description">{matter.description}</DetailValue>
      </dl>
    </aside>
  );
}
