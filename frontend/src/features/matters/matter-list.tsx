"use client";

import Link from "next/link";
import { useState } from "react";
import { Field, LoadError, PageHeading, Pagination } from "@/components/workspace-ui";
import { Icon } from "@/components/icon";
import { StatusPill } from "@/components/status-pill";
import { useAdminQuery } from "@/features/auth/use-admin-query";
import { ClientPicker } from "@/features/clients/client-picker";
import type { ClientChoice } from "@/features/clients/client-types";
import { apiPath } from "@/lib/api";
import type { PagedResult } from "@/lib/pagination";
import { useListSearch } from "@/lib/use-list-search";
import { archiveStates, formatCalendarDate, matterPriorities, matterStatuses,
  type ArchiveState, type Matter, type MatterPriority, type MatterStatus } from "./matter-types";

export function MatterList() {
  const search = useListSearch();
  const [client, setClient] = useState<ClientChoice | null>(null);
  const [status, setStatus] = useState<MatterStatus | "">("");
  const [priority, setPriority] = useState<MatterPriority | "">("");
  const [archiveState, setArchiveState] = useState<ArchiveState>("Active");
  const [expanded, setExpanded] = useState(false);
  const { data, error, pending, refresh } = useAdminQuery<PagedResult<Matter>>(apiPath("/matters", {
    search: search.search, clientId: client?.id ?? "", status, priority, archiveState, page: search.page,
  }));
  const facets = Number(!!client) + Number(!!status) + Number(!!priority);
  const narrowed = !!(facets || search.draft);
  function reset() {
    search.clear();
    setClient(null);
    setStatus("");
    setPriority("");
  }
  return <>
    <PageHeading title="Matters" description="Manage your firm’s matters, from opening to close."
      action={<Link href="/matters/new" className="admin-primary"><Icon name="plus" size={16} />New matter</Link>} />
    <div className="register">
      <div className="register-toolbar">
        <div className="segmented" role="group" aria-label="Archive state">
          {(Object.keys(archiveStates) as ArchiveState[]).map((state) =>
            <button key={state} aria-pressed={archiveState === state} onClick={() => { setArchiveState(state); search.setPage(1); }}>
              {archiveStates[state]}
            </button>)}
        </div>
        <div className="search-tools">
          <label className="search-box"><Icon name="search" size={17} /><span className="sr-only">Search matters</span>
            <input type="search" maxLength={200} placeholder="Search matters…" value={search.draft} onChange={(e) => search.setDraft(e.target.value)} />
          </label>
          <button className="admin-secondary" aria-expanded={expanded} aria-controls="matter-filters"
            onClick={() => setExpanded(!expanded)}><Icon name="filter" size={16} />Filters{facets > 0 && <span className="filter-count">{facets}</span>}</button>
        </div>
      </div>
      <div id="matter-filters" hidden={!expanded} className="filter-fields">
        <Field id="filter-client" label="Client"><ClientPicker id="filter-client" value={client} placeholder="All clients"
          onChange={(value) => { setClient(value); search.setPage(1); }} /></Field>
        <Field id="filter-status" label="Status"><select id="filter-status" className="admin-input" value={status}
          onChange={(e) => { setStatus(e.target.value as typeof status); search.setPage(1); }}>
          <option value="">Any status</option>{Object.entries(matterStatuses).map(([key, label]) => <option key={key} value={key}>{label}</option>)}
        </select></Field>
        <Field id="filter-priority" label="Priority"><select id="filter-priority" className="admin-input" value={priority}
          onChange={(e) => { setPriority(e.target.value as typeof priority); search.setPage(1); }}>
          <option value="">Any priority</option>{Object.entries(matterPriorities).map(([key, label]) => <option key={key} value={key}>{label}</option>)}
        </select></Field>
      </div>
      <div className="register-caption"><h2>{archiveState === "All" ? "All" : archiveStates[archiveState]} matters
        {data && <span className="result-count">{data.totalCount}</span>}</h2>
        {narrowed && <button className="text-action" onClick={reset}>Clear filters</button>}
      </div>
      {pending && <p role="status" className="feed-message">Loading matters…</p>}
      {error && <LoadError error={error} resource="matters" onRetry={refresh} />}
      {data && (data.items.length ? <table className="matter-table">
        <caption className="sr-only">Matter register</caption><thead><tr>
          {["Matter", "Client", "Status", "Priority", "Key dates"].map((name) => <th key={name} scope="col">{name}</th>)}
        </tr></thead>
        <tbody>{data.items.map((matter) => <MatterRow key={matter.id} matter={matter} />)}</tbody>
      </table> : <div className="empty-state"><span className="item-icon"><Icon name="folder" size={24} /></span>
        <p role="status">{narrowed ? "No matters match these filters." : `No ${archiveState === "All" ? "" : archiveState.toLowerCase() + " "}matters to display.`}</p>
        {narrowed && <button className="admin-secondary" onClick={reset}>Show all in this view</button>}
      </div>)}
    </div>
    {data && <Pagination result={data} onPage={search.setPage} />}
  </>;
}

function MatterRow({ matter }: { matter: Matter }) {
  return <tr>
    <td><div className="matter-name"><span className="item-icon"><Icon name="folder" /></span><div className="min-w-0">
      <Link className="record-link" href={`/matters/${matter.id}`}>{matter.title}</Link>
      <small>{matter.referenceNumber}{matter.archivedAtUtc && <> · <StatusPill value="Archived" /></>}</small>
    </div></div></td>
    <td data-label="Client">{matter.primaryClientName}</td>
    <td data-label="Status"><StatusPill value={matterStatuses[matter.status]} /></td>
    <td data-label="Priority"><StatusPill value={matterPriorities[matter.priority]} /></td>
    <td data-label="Key dates"><span>Opened {formatCalendarDate(matter.openedDate)}</span>
      {matter.targetCloseDate && <small>Target {formatCalendarDate(matter.targetCloseDate)}</small>}</td>
  </tr>;
}
