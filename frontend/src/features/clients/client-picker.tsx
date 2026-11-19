"use client";

import { useEffect, useId, useRef, useState } from "react";
import { fieldDescription, LoadError, Pagination } from "@/components/workspace-ui";
import { useAdminQuery } from "@/features/auth/use-admin-query";
import { apiPath } from "@/lib/api";
import type { PagedResult } from "@/lib/pagination";
import type { Client, ClientChoice } from "./client-types";

export function ClientPicker({ id, value, onChange, error, placeholder = "Choose a client" }: {
  id: string;
  value: ClientChoice | null;
  onChange: (client: ClientChoice | null) => void;
  error?: string;
  placeholder?: string;
}) {
  const [open, setOpen] = useState(false);
  const [draft, setDraft] = useState("");
  const [filter, setFilter] = useState({ search: "", page: 1 });
  const button = useRef<HTMLButtonElement>(null);
  const input = useRef<HTMLInputElement>(null);
  const panelId = useId();
  const results = useAdminQuery<PagedResult<Client>>(open ? apiPath("/clients", filter) : null);

  useEffect(() => {
    if (open) input.current?.focus();
  }, [open]);

  useEffect(() => {
    if (!open) return;
    const timer = window.setTimeout(() => {
      setFilter((current) => current.search === draft.trim() ? current : { search: draft.trim(), page: 1 });
    }, 200);
    return () => window.clearTimeout(timer);
  }, [draft, open]);

  function close() {
    setOpen(false);
    button.current?.focus();
  }

  function select(client: ClientChoice | null) {
    onChange(client);
    setDraft("");
    setFilter({ search: "", page: 1 });
    close();
  }

  return (
    <div className="relative" onBlur={(event) => {
      if (!event.currentTarget.contains(event.relatedTarget)) setOpen(false);
    }} onKeyDown={(event) => {
      if (event.key === "Escape" && open) {
        event.preventDefault();
        close();
      }
    }}>
      <button type="button" id={id} ref={button} className="admin-input flex items-center justify-between gap-3 text-left"
        aria-expanded={open} aria-controls={open ? panelId : undefined} {...fieldDescription(id, error)}
        onClick={() => setOpen((current) => !current)}>
        <span className="min-w-0 truncate">{value?.name ?? placeholder}</span>
        <span aria-hidden="true" className="shrink-0 text-sm text-neutral-600">⌄</span>
      </button>
      {open && (
        <div id={panelId} className="admin-panel absolute top-full z-10 mt-2 w-full min-w-0 p-3 sm:min-w-72">
          <label htmlFor={`${panelId}-search`} className="block text-sm font-medium">Find a client</label>
          <input id={`${panelId}-search`} ref={input} type="search" value={draft} maxLength={200}
            className="admin-input" onChange={(event) => setDraft(event.target.value)}
            onKeyDown={(event) => { if (event.key === "Enter") event.preventDefault(); }} />
          {value && (
            <button type="button" className="admin-link mt-3 text-sm" onClick={() => select(null)}>Clear client</button>
          )}
          <div className="mt-3">
            {results.pending && <p role="status" className="text-sm text-neutral-600">Loading clients…</p>}
            {results.error && <LoadError error={results.error} resource="clients" onRetry={results.refresh} compact />}
            {results.data?.items.length === 0 && <p role="status" className="text-sm text-neutral-700">No matching clients.</p>}
            {results.data && (
              <>
                <ul className="max-h-48 space-y-1 overflow-auto p-1">
                  {results.data.items.map((client) => (
                    <li key={client.id}>
                      <button type="button" onClick={() => select({ id: client.id, name: client.name })}
                        className="w-full rounded-md px-2 py-2 text-left text-sm wrap-anywhere hover:bg-stone-100 focus-visible:outline-2 focus-visible:outline-red-700">
                        <span className="block font-medium">{client.name}</span>
                        <span className="block text-neutral-600">{client.email}</span>
                      </button>
                    </li>
                  ))}
                </ul>
                <Pagination result={results.data} onPage={(page) => setFilter((current) => ({ ...current, page }))} />
              </>
            )}
          </div>
        </div>
      )}
    </div>
  );
}
