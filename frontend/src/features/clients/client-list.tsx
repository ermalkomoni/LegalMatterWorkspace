"use client";

import Link from "next/link";
import { useState, type FormEvent } from "react";
import { LoadError, PageHeading, Pagination } from "@/components/workspace-ui";
import { useAdminQuery } from "@/features/auth/use-admin-query";
import { apiPath } from "@/lib/api";
import type { PagedResult } from "@/lib/pagination";
import type { Client } from "./client-types";

export function ClientList() {
  const [draft, setDraft] = useState("");
  const [search, setSearch] = useState("");
  const [page, setPage] = useState(1);
  const { data, error, pending, refresh } = useAdminQuery<PagedResult<Client>>(
    apiPath("/clients", { search, page }),
  );

  function submit(event: FormEvent<HTMLFormElement>) {
    event.preventDefault();
    setPage(1);
    setSearch(draft.trim());
  }

  function clear() {
    setDraft("");
    setSearch("");
    setPage(1);
  }

  return (
    <>
      <PageHeading title="Clients" action={<Link className="admin-primary" href="/clients/new">New client</Link>} />
      <form onSubmit={submit} className="admin-panel mb-6 flex flex-wrap items-end gap-3 p-4 sm:p-5">
        <div className="min-w-0 flex-1 basis-56">
          <label htmlFor="client-search" className="block text-sm font-medium">Search clients</label>
          <input id="client-search" type="search" className="admin-input" maxLength={200}
            value={draft} onChange={(event) => setDraft(event.target.value)} />
        </div>
        <button type="submit" className="admin-secondary">Search</button>
        <button type="button" className="admin-secondary" onClick={clear}>Clear search</button>
      </form>

      {pending && <p role="status" className="p-4 text-sm text-neutral-600">Loading clients…</p>}
      {error && <LoadError error={error} resource="clients" onRetry={refresh} />}
      {data && (
        <>
          {data.items.length > 0 ? (
            <ul className="admin-panel divide-y divide-stone-200">
              {data.items.map((client) => (
                <li key={client.id} className="grid gap-1 px-4 py-4 sm:grid-cols-[1.2fr_1.2fr_0.75fr] sm:gap-5 sm:px-5">
                  <div className="min-w-0 wrap-anywhere">
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
