"use client";

import { useRouter } from "next/navigation";
import { useState } from "react";
import { BackLink, DetailValue, Feedback, LoadError, PageHeading } from "@/components/workspace-ui";
import { useAdminQuery } from "@/features/auth/use-admin-query";
import { ClientEditor } from "./client-editor";
import type { Client } from "./client-types";

export function NewClient() {
  const router = useRouter();
  return (
    <>
      <BackLink href="/clients">Clients</BackLink>
      <PageHeading title="New client" />
      <ClientEditor onSaved={(client) => router.replace(`/clients/${client.id}?created=1`)}
        onCancel={() => router.replace("/clients")} />
    </>
  );
}

export function ClientDetails({ id, created }: { id: number; created: boolean }) {
  const { data, error, pending, refresh } = useAdminQuery<Client>(`/clients/${id}`);
  const [editing, setEditing] = useState(false);
  const [notice, setNotice] = useState(created ? "Client created." : "");

  return (
    <>
      <BackLink href="/clients">Clients</BackLink>
      <PageHeading title={editing ? "Edit client" : data?.name ?? "Client details"}
        action={data && !editing && (
          <button type="button" className="admin-secondary" onClick={() => setEditing(true)}>Edit client</button>
        )} />
      <div className="space-y-4">
        {notice && !editing && <Feedback>{notice}</Feedback>}
        {pending && <p role="status" className="text-sm text-neutral-600">{data ? "Refreshing client…" : "Loading client…"}</p>}
        {error && <LoadError error={error} resource="Client" onRetry={refresh} />}
        {data && (editing ? (
          <ClientEditor client={data} onCancel={() => setEditing(false)} onSaved={(saved) => {
            refresh(saved);
            setNotice("Client updated.");
            setEditing(false);
          }} />
        ) : (
          <dl className="admin-panel grid gap-5 p-5 sm:grid-cols-2 sm:p-6">
            <DetailValue label="Email">{data.email}</DetailValue>
            <DetailValue label="Phone">{data.phone}</DetailValue>
            <DetailValue label="Postal address">{data.postalAddress}</DetailValue>
          </dl>
        ))}
      </div>
    </>
  );
}
