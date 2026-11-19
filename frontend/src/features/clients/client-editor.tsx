"use client";

import { useState, type FormEvent } from "react";
import { Feedback, Field, fieldDescription } from "@/components/workspace-ui";
import { useAdminSession } from "@/features/auth/admin-session";
import { ApiError, jsonRequest, type FieldErrors } from "@/lib/api";
import type { Client, ClientInput } from "./client-types";

const emptyClient: ClientInput = { name: "", email: "", phone: null, postalAddress: null };

export function ClientEditor({ client, onSaved, onCancel }: {
  client?: Client;
  onSaved: (client: Client) => void;
  onCancel: () => void;
}) {
  const { request } = useAdminSession();
  const [draft, setDraft] = useState<ClientInput>(client ?? emptyClient);
  const [errors, setErrors] = useState<FieldErrors>({});
  const [feedback, setFeedback] = useState("");
  const [saving, setSaving] = useState(false);

  function change(key: keyof ClientInput, value: string) {
    setDraft((current) => ({ ...current, [key]: value }));
  }

  async function submit(event: FormEvent<HTMLFormElement>) {
    event.preventDefault();
    if (saving) return;
    setSaving(true);
    setErrors({});
    setFeedback("");
    const payload: ClientInput = {
      name: draft.name.trim(),
      email: draft.email.trim(),
      phone: draft.phone?.trim() || null,
      postalAddress: draft.postalAddress?.trim() || null,
    };

    try {
      const saved = await request<Client>(
        client ? `/clients/${client.id}` : "/clients",
        jsonRequest(client ? "PUT" : "POST", payload),
      );
      onSaved(saved);
    } catch (error) {
      if (error instanceof ApiError && error.status === 400) {
        setErrors(error.fieldErrors);
        setFeedback("Check the highlighted fields and try again.");
      } else {
        setFeedback("Couldn’t save the client. Try again.");
      }
    } finally {
      setSaving(false);
    }
  }

  return (
    <form method="post" onSubmit={submit} className="admin-panel grid gap-5 p-5 sm:grid-cols-2 sm:p-6">
      <Field id="client-name" label="Name" error={errors.name?.[0]}>
        <input id="client-name" className="admin-input" required maxLength={200}
          value={draft.name} onChange={(event) => change("name", event.target.value)}
          {...fieldDescription("client-name", errors.name?.[0])} />
      </Field>
      <Field id="client-email" label="Email" error={errors.email?.[0]}>
        <input id="client-email" type="email" className="admin-input" required maxLength={256}
          value={draft.email} onChange={(event) => change("email", event.target.value)}
          {...fieldDescription("client-email", errors.email?.[0])} />
      </Field>
      <Field id="client-phone" label="Phone (optional)" error={errors.phone?.[0]}>
        <input id="client-phone" type="tel" className="admin-input" maxLength={50}
          value={draft.phone ?? ""} onChange={(event) => change("phone", event.target.value)}
          {...fieldDescription("client-phone", errors.phone?.[0])} />
      </Field>
      <div className="sm:col-span-2">
        <Field id="client-address" label="Postal address (optional)" error={errors.postaladdress?.[0]}>
          <textarea id="client-address" className="admin-input" rows={3} maxLength={500}
            value={draft.postalAddress ?? ""} onChange={(event) => change("postalAddress", event.target.value)}
            {...fieldDescription("client-address", errors.postaladdress?.[0])} />
        </Field>
      </div>
      {feedback && <div className="sm:col-span-2"><Feedback kind="error">{feedback}</Feedback></div>}
      <div className="flex flex-wrap gap-3 sm:col-span-2">
        <button type="submit" className="admin-primary" disabled={saving}>
          {saving ? "Saving…" : client ? "Save changes" : "Create client"}
        </button>
        <button type="button" className="admin-secondary" onClick={onCancel} disabled={saving}>Cancel</button>
      </div>
    </form>
  );
}
