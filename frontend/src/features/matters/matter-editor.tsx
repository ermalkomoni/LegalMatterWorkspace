"use client";

import { useState, type FormEvent } from "react";
import { Feedback, Field, fieldDescription } from "@/components/workspace-ui";
import { useAdminSession } from "@/features/auth/admin-session";
import { ClientPicker } from "@/features/clients/client-picker";
import type { ClientChoice } from "@/features/clients/client-types";
import { ApiError, apiPath, jsonRequest, type FieldErrors } from "@/lib/api";
import type { PagedResult } from "@/lib/pagination";
import {
  matterStatuses, matterPriorities,
  type Matter, type MatterInput, type MatterStatus, type MatterPriority,
} from "./matter-types";

const emptyMatter: MatterInput = {
  referenceNumber: "",
  title: "",
  description: "",
  primaryClientId: 0,
  status: "Open",
  priority: "Normal",
  openedDate: "",
  targetCloseDate: null,
};

const duplicateReference = "A matter with this reference number already exists.";

export function MatterEditor({ matter, onSaved, onCancel, onArchived }: {
  matter?: Matter;
  onSaved: (matter: Matter) => void;
  onCancel: () => void;
  onArchived?: (matter: Matter) => void;
}) {
  const { request } = useAdminSession();
  const [draft, setDraft] = useState<MatterInput>(matter ?? emptyMatter);
  const [client, setClient] = useState<ClientChoice | null>(
    matter ? { id: matter.primaryClientId, name: matter.primaryClientName } : null,
  );
  const [errors, setErrors] = useState<FieldErrors>({});
  const [feedback, setFeedback] = useState("");
  const [saving, setSaving] = useState(false);

  function change<K extends keyof MatterInput>(key: K, value: MatterInput[K]) {
    setDraft((current) => ({ ...current, [key]: value }));
  }

  async function resolveConflict(reference: string) {
    if (!matter) {
      setErrors({ referencenumber: [duplicateReference] });
      return;
    }

    // The API uses a generic 409 for duplicate references and concurrent changes.
    try {
      const latest = await request<Matter>(`/matters/${matter.id}`);
      if (latest.archivedAtUtc) {
        onArchived?.(latest);
        return;
      }
      if (reference.toLowerCase() !== latest.referenceNumber.toLowerCase()) {
        let page = 1;
        for (;;) {
          const result = await request<PagedResult<Matter>>(
            apiPath("/matters", { search: reference, archiveState: "All", page, pageSize: 100 }),
          );
          if (result.items.some((item) => item.id !== matter.id && item.referenceNumber.toLowerCase() === reference.toLowerCase())) {
            setErrors({ referencenumber: [duplicateReference] });
            return;
          }
          if (page * result.pageSize >= result.totalCount) break;
          page += 1;
        }
      }
    } catch {
      setFeedback("Couldn’t check the conflicting change. Reload the matter and try again.");
      return;
    }
    setFeedback("The matter changed during the request. Reload it and try again.");
  }

  async function submit(event: FormEvent<HTMLFormElement>) {
    event.preventDefault();
    if (saving) return;
    setErrors({});
    setFeedback("");

    const validation: FieldErrors = {};
    if (!client) validation.primaryclientid = ["Choose a client."];
    if (draft.targetCloseDate && draft.targetCloseDate < draft.openedDate) {
      validation.targetclosedate = ["Target close date cannot precede the opened date."];
    }
    if (!client || validation.targetclosedate) {
      setErrors(validation);
      return;
    }

    const payload: MatterInput = {
      referenceNumber: draft.referenceNumber.trim(),
      title: draft.title.trim(),
      description: draft.description.trim(),
      primaryClientId: client.id,
      status: draft.status,
      priority: draft.priority,
      openedDate: draft.openedDate,
      targetCloseDate: draft.targetCloseDate || null,
    };

    setSaving(true);
    try {
      const saved = await request<Matter>(
        matter ? `/matters/${matter.id}` : "/matters",
        jsonRequest(matter ? "PUT" : "POST", payload),
      );
      onSaved(saved);
    } catch (error) {
      if (error instanceof ApiError && error.status === 409) {
        await resolveConflict(payload.referenceNumber);
      } else if (error instanceof ApiError && error.status === 400) {
        setErrors(error.fieldErrors);
        setFeedback("Check the highlighted fields and try again.");
      } else {
        setFeedback("Couldn’t save the matter. Try again.");
      }
    } finally {
      setSaving(false);
    }
  }

  return (
    <form method="post" onSubmit={submit} className="admin-panel grid gap-5 p-5 sm:grid-cols-2 sm:p-6">
      <Field id="matter-reference" label="Reference number" error={errors.referencenumber?.[0]}>
        <input id="matter-reference" className="admin-input" required maxLength={50}
          value={draft.referenceNumber} onChange={(event) => change("referenceNumber", event.target.value)}
          {...fieldDescription("matter-reference", errors.referencenumber?.[0])} />
      </Field>
      <Field id="matter-title" label="Title" error={errors.title?.[0]}>
        <input id="matter-title" className="admin-input" required maxLength={200}
          value={draft.title} onChange={(event) => change("title", event.target.value)}
          {...fieldDescription("matter-title", errors.title?.[0])} />
      </Field>
      <div className="sm:col-span-2">
        <Field id="matter-description" label="Description" error={errors.description?.[0]}>
          <textarea id="matter-description" className="admin-input" rows={4} required maxLength={5000}
            value={draft.description} onChange={(event) => change("description", event.target.value)}
            {...fieldDescription("matter-description", errors.description?.[0])} />
        </Field>
      </div>
      <div className="sm:col-span-2">
        <Field id="primary-client" label="Primary client" error={errors.primaryclientid?.[0]}>
          <ClientPicker id="primary-client" value={client} onChange={setClient} error={errors.primaryclientid?.[0]} />
        </Field>
      </div>
      <Field id="matter-status" label="Status" error={errors.status?.[0]}>
        <select id="matter-status" className="admin-input" value={draft.status}
          onChange={(event) => change("status", event.target.value as MatterStatus)}
          {...fieldDescription("matter-status", errors.status?.[0])}>
          {Object.entries(matterStatuses).map(([value, label]) => <option key={value} value={value}>{label}</option>)}
        </select>
      </Field>
      <Field id="matter-priority" label="Priority" error={errors.priority?.[0]}>
        <select id="matter-priority" className="admin-input" value={draft.priority}
          onChange={(event) => change("priority", event.target.value as MatterPriority)}
          {...fieldDescription("matter-priority", errors.priority?.[0])}>
          {Object.entries(matterPriorities).map(([value, label]) => <option key={value} value={value}>{label}</option>)}
        </select>
      </Field>
      <Field id="matter-opened" label="Opened date" error={errors.openeddate?.[0]}>
        <input id="matter-opened" type="date" className="admin-input" required min="0001-01-01" max="9999-12-31"
          value={draft.openedDate} onChange={(event) => change("openedDate", event.target.value)}
          {...fieldDescription("matter-opened", errors.openeddate?.[0])} />
      </Field>
      <Field id="matter-target" label="Target close date (optional)" error={errors.targetclosedate?.[0]}>
        <input id="matter-target" type="date" className="admin-input" min="0001-01-01" max="9999-12-31"
          value={draft.targetCloseDate ?? ""} onChange={(event) => change("targetCloseDate", event.target.value || null)}
          {...fieldDescription("matter-target", errors.targetclosedate?.[0])} />
      </Field>
      {feedback && <div className="sm:col-span-2"><Feedback kind="error">{feedback}</Feedback></div>}
      <div className="flex flex-wrap gap-3 sm:col-span-2">
        <button type="submit" className="admin-primary" disabled={saving}>
          {saving ? "Saving…" : matter ? "Save changes" : "Create matter"}
        </button>
        <button type="button" className="admin-secondary" onClick={onCancel} disabled={saving}>Cancel</button>
      </div>
    </form>
  );
}
