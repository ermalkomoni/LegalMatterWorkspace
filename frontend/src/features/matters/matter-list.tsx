"use client";

import Link from "next/link";
import { useState, type FormEvent } from "react";
import { Field, LoadError, PageHeading, Pagination } from "@/components/workspace-ui";
import { useAdminQuery } from "@/features/auth/use-admin-query";
import { ClientPicker } from "@/features/clients/client-picker";
import type { ClientChoice } from "@/features/clients/client-types";
import { apiPath } from "@/lib/api";
import type { PagedResult } from "@/lib/pagination";
import {
  archiveStates, formatCalendarDate, matterPriorities, matterStatuses,
  type ArchiveState, type Matter, type MatterPriority, type MatterStatus,
} from "./matter-types";

type Filters = {
  search: string;
  client: ClientChoice | null;
  status: MatterStatus | "";
  priority: MatterPriority | "";
  archiveState: ArchiveState;
};

const initialFilters: Filters = { search: "", client: null, status: "", priority: "", archiveState: "Active" };

export function MatterList() {
  const [draft, setDraft] = useState(initialFilters);
  const [filters, setFilters] = useState(initialFilters);
  const [page, setPage] = useState(1);
  const { data, error, pending, refresh } = useAdminQuery<PagedResult<Matter>>(apiPath("/matters", {
    search: filters.search,
    clientId: filters.client?.id ?? "",
    status: filters.status,
    priority: filters.priority,
    archiveState: filters.archiveState,
    page,
  }));
  const narrowed = Boolean(filters.search || filters.client || filters.status || filters.priority || filters.archiveState !== "Active");

  function update<K extends keyof Filters>(key: K, value: Filters[K]) {
    setDraft((current) => ({ ...current, [key]: value }));
  }

  function submit(event: FormEvent<HTMLFormElement>) {
    event.preventDefault();
    setPage(1);
    setFilters({ ...draft, search: draft.search.trim() });
  }

  function clear() {
    setDraft(initialFilters);
    setFilters(initialFilters);
    setPage(1);
  }

  return (
    <>
      <PageHeading title="Matters" action={<Link href="/matters/new" className="admin-primary">New matter</Link>} />
      <form onSubmit={submit} className="admin-panel mb-6 grid gap-4 p-4 sm:grid-cols-2 sm:p-5 lg:grid-cols-4">
        <div className="sm:col-span-2 lg:col-span-4">
          <Field id="matter-search" label="Search matters">
            <input id="matter-search" type="search" className="admin-input" maxLength={200}
              placeholder="Reference, title, or client" value={draft.search}
              onChange={(event) => update("search", event.target.value)} />
          </Field>
        </div>
        <Field id="filter-client" label="Client">
          <ClientPicker id="filter-client" value={draft.client} onChange={(client) => update("client", client)}
            placeholder="All clients" />
        </Field>
        <Field id="filter-status" label="Status">
          <select id="filter-status" className="admin-input" value={draft.status}
            onChange={(event) => update("status", event.target.value as Filters["status"])}>
            <option value="">Any status</option>
            {Object.entries(matterStatuses).map(([value, label]) => <option key={value} value={value}>{label}</option>)}
          </select>
        </Field>
        <Field id="filter-priority" label="Priority">
          <select id="filter-priority" className="admin-input" value={draft.priority}
            onChange={(event) => update("priority", event.target.value as Filters["priority"])}>
            <option value="">Any priority</option>
            {Object.entries(matterPriorities).map(([value, label]) => <option key={value} value={value}>{label}</option>)}
          </select>
        </Field>
        <Field id="filter-archive" label="Archive state">
          <select id="filter-archive" className="admin-input" value={draft.archiveState}
            onChange={(event) => update("archiveState", event.target.value as ArchiveState)}>
            {Object.entries(archiveStates).map(([value, label]) => <option key={value} value={value}>{label}</option>)}
          </select>
        </Field>
        <div className="flex flex-wrap gap-3 sm:col-span-2 lg:col-span-4">
          <button type="submit" className="admin-primary">Apply filters</button>
          <button type="button" className="admin-secondary" onClick={clear}>Clear filters</button>
        </div>
      </form>

      {pending && <p role="status" className="p-4 text-sm text-neutral-600">Loading matters…</p>}
      {error && <LoadError error={error} resource="matters" onRetry={refresh} />}
      {data && (
        <>
          {data.items.length > 0 ? (
            <ul className="admin-panel divide-y divide-stone-200">
              {data.items.map((matter) => <MatterRow key={matter.id} matter={matter} />)}
            </ul>
          ) : (
            <div className="admin-panel p-6">
              <p role="status" className="font-medium">{narrowed ? "No matters match these filters." : "No active matters to display."}</p>
            </div>
          )}
          <Pagination result={data} onPage={setPage} />
        </>
      )}
    </>
  );
}

function MatterRow({ matter }: { matter: Matter }) {
  return (
    <li className="grid gap-4 px-4 py-4 sm:px-5 md:grid-cols-[1.15fr_1fr] md:gap-8">
      <div className="min-w-0">
        <p className="text-xs font-semibold tracking-wide text-neutral-600 wrap-anywhere">{matter.referenceNumber}</p>
        <Link href={`/matters/${matter.id}`} className="admin-link mt-1 inline-block wrap-anywhere">{matter.title}</Link>
        <p className="mt-1 text-sm text-neutral-700 wrap-anywhere">{matter.primaryClientName}</p>
        {matter.archivedAtUtc && (
          <span className="mt-2 inline-block rounded-md bg-stone-100 px-2 py-1 text-xs font-semibold">Archived</span>
        )}
      </div>
      <dl className="grid grid-cols-2 gap-x-3 gap-y-2 text-sm sm:grid-cols-4 md:grid-cols-2 lg:grid-cols-4">
        <div><dt className="text-xs text-neutral-600">Status</dt><dd>{matterStatuses[matter.status]}</dd></div>
        <div><dt className="text-xs text-neutral-600">Priority</dt><dd>{matterPriorities[matter.priority]}</dd></div>
        <div><dt className="text-xs text-neutral-600">Opened</dt><dd>{formatCalendarDate(matter.openedDate)}</dd></div>
        <div>
          <dt className="text-xs text-neutral-600">Target close</dt>
          <dd>{matter.targetCloseDate ? formatCalendarDate(matter.targetCloseDate) : "—"}</dd>
        </div>
      </dl>
    </li>
  );
}
