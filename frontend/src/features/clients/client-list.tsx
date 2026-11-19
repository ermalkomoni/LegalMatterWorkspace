"use client";

import { useListSearch } from "@/lib/use-list-search";
import { Icon } from "@/components/icon";
import Link from "next/link";
import { LoadError, PageHeading, Pagination } from "@/components/workspace-ui";
import { useAdminQuery } from "@/features/auth/use-admin-query";
import { apiPath } from "@/lib/api";
import type { PagedResult } from "@/lib/pagination";
import type { Client } from "./client-types";

export function ClientList() {
  const { draft, setDraft, search, page, setPage, clear } = useListSearch();
  const { data, error, pending, refresh } = useAdminQuery<PagedResult<Client>>(
    apiPath("/clients", { search, page }),
  );

  return (
    <>
      <PageHeading title="Clients" description="Find a client and manage their contact details." action={<Link className="admin-primary" href="/clients/new"><Icon name="plus" size={16} />New client</Link>} />
      <div className="directory-tools">
        <label className="search-box"><Icon name="search" size={17} /><span className="sr-only">Search clients</span>
          <input type="search" maxLength={200} value={draft} placeholder="Search clients…" onChange={(e) => setDraft(e.target.value)} />
        </label>
        {draft && <button type="button" className="text-action" onClick={clear}>Clear search</button>}
        {data && <span className="directory-count">{data.totalCount} {data.totalCount === 1 ? "client" : "clients"}</span>}
      </div>

      {pending && <p role="status" className="p-4 text-sm text-neutral-600">Loading clients…</p>}
      {error && <LoadError error={error} resource="clients" onRetry={refresh} />}
      {data && (
        <>
          {data.items.length > 0 ? (
            <ul className="admin-panel divide-y divide-stone-200">
              {data.items.map((client) => (
                <li key={client.id} className="grid gap-1 px-4 py-4 sm:grid-cols-[1.2fr_1.2fr_0.75fr] sm:gap-5 sm:px-5">
                  <div className="flex min-w-0 items-center gap-3 wrap-anywhere"><span className="client-initial" aria-hidden="true">{client.name.trim().charAt(0)}</span>
                    <Link href={`/clients/${client.id}`} className="admin-link">{client.name}</Link>
                  </div>
                  <p className="min-w-0 text-sm text-neutral-700 wrap-anywhere">{client.email}</p>
                  {client.phone && <p className="min-w-0 text-sm text-neutral-700 wrap-anywhere">{client.phone}</p>}
                </li>
              ))}
            </ul>
          ) : (
            <div className="admin-panel p-6">
              <p role="status" className="font-medium">{search ? "No clients match your search." : "No clients to display."}</p>
            </div>
          )}
          <Pagination result={data} onPage={setPage} />
        </>
      )}
    </>
  );
}
