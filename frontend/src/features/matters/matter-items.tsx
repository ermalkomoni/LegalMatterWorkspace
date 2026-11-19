import { useState } from "react";
import { Feedback, LoadError } from "@/components/workspace-ui";
import { useAdminSession } from "@/features/auth/admin-session";
import { useAdminQuery } from "@/features/auth/use-admin-query";
import { ApiError, jsonRequest, type ApiPath, type FieldErrors } from "@/lib/api";
import { MatterTimestamp } from "./matter-history";
import { MatterItemForm, type ItemField } from "./matter-item-form";
import { formatCalendarDate } from "./matter-types";

type WorkItem = { id: number; title: string; status: "Pending" | "Completed"; isOverdue: boolean };
type MatterTask = WorkItem & { description: string | null; dueDate: string | null };
type MatterDeadline = WorkItem & { date: string };
type MatterNote = { id: number; text: string; createdAtUtc: string; updatedAtUtc: string };
type MatterItem = MatterTask | MatterDeadline | MatterNote;

const titleField = { name: "title", label: "Title", type: "text", required: true, maxLength: 200 } as const;

export const matterCollections = {
  tasks: {
    title: "Tasks", itemName: "task",
    fields: [titleField,
      { name: "description", label: "Description (optional)", type: "textarea", maxLength: 5000 },
      { name: "dueDate", label: "Due date (optional)", type: "date" }],
  },
  deadlines: {
    title: "Deadlines", itemName: "deadline",
    fields: [titleField, { name: "date", label: "Deadline date", type: "date", required: true }],
  },
  notes: {
    title: "Internal notes", itemName: "note",
    fields: [{ name: "text", label: "Note", type: "textarea", required: true, maxLength: 10000 }],
  },
} as const satisfies Record<string, { title: string; itemName: string; fields: readonly ItemField[] }>;

export type MatterCollection = keyof typeof matterCollections;
type Interaction = { type: "create" } | { type: "edit" | "delete"; itemId: number };
type ItemFeedback = { kind: "error" | "status"; message: string; itemId?: number; errors?: FieldErrors };

export function MatterItems({ matterId, collection, readOnly, onChange, onConflict }: {
  matterId: number;
  collection: MatterCollection;
  readOnly: boolean;
  onChange: () => void;
  onConflict: () => void;
}) {
  const { title, itemName, fields } = matterCollections[collection];
  const { request } = useAdminSession();
  const path = `/matters/${matterId}/${collection}` as const;
  const { data, pending, error, refresh } = useAdminQuery<MatterItem[]>(path);
  const [interaction, setInteraction] = useState<Interaction | null>(null);
  const [busy, setBusy] = useState(false);
  const [feedback, setFeedback] = useState<ItemFeedback | null>(null);
  const addId = `add-${collection}`;

  function editId(itemId: number) {
    return `edit-${collection}-${itemId}`;
  }

  function finishInteraction(itemId?: number) {
    setInteraction(null);
    requestAnimationFrame(() => {
      const target = itemId === undefined ? null : document.getElementById(editId(itemId));
      (target ?? document.getElementById(addId))?.focus();
    });
  }

  function beginInteraction(next: Interaction) {
    setFeedback(null);
    setInteraction(next);
  }

  async function mutate(endpoint: ApiPath, options: RequestInit, success: string, failure: string, itemId?: number) {
    if (busy || readOnly) return;
    setBusy(true);
    setFeedback(null);
    try {
      const saved = await request<MatterItem | undefined>(endpoint, options);
      const deleted = options.method === "DELETE";
      finishInteraction(deleted ? undefined : itemId);
      setFeedback({ kind: "status", message: success, itemId: deleted ? undefined : itemId });
      if (data && saved) {
        refresh(itemId === undefined ? [saved, ...data] : data.map((item) => item.id === itemId ? saved : item));
      } else {
        refresh(data?.filter((item) => item.id !== itemId));
      }
      onChange();
    } catch (error) {
      let message = failure;
      let errors: FieldErrors | undefined;
      if (error instanceof ApiError) {
        if (error.status === 400 && Object.keys(error.fieldErrors).length > 0) {
          errors = error.fieldErrors;
          message = "Check the highlighted fields and try again.";
        }
        if (error.status === 409) {
          message = "The matter changed during the request. Review the current details and try again.";
          refresh();
          onConflict();
        }
        if (error.status === 404) {
          message = `This ${itemName} could not be found. Check the current list.`;
          finishInteraction();
          refresh();
          onConflict();
          itemId = undefined;
        }
      }
      setFeedback({ kind: "error", message, itemId, errors });
    } finally {
      setBusy(false);
    }
  }

  const blocked = busy || interaction !== null;
  const creating = !readOnly && interaction?.type === "create";

  return (
    <section id={collection} aria-labelledby={`${collection}-heading`} tabIndex={-1} className="admin-panel scroll-mt-4">
      <div className="space-y-3 p-4 sm:px-6 sm:py-5">
        <div className="flex flex-wrap items-center justify-between gap-x-4 gap-y-2">
          <h2 id={`${collection}-heading`} className="text-xl font-semibold tracking-tight">{title}</h2>
          {!readOnly && <button id={addId} type="button" className="admin-secondary" disabled={blocked}
            onClick={() => beginInteraction({ type: "create" })}>Add {itemName}</button>}
        </div>
        {pending && <p role="status" className="text-sm text-neutral-600">{data ? "Refreshing" : "Loading"} {title.toLowerCase()}…</p>}
        {error && <LoadError error={error} resource={title.toLowerCase()} onRetry={refresh} compact />}
        {data?.length === 0 && !creating && <p className="text-sm text-neutral-600">No {title.toLowerCase()} yet.</p>}
        {feedback && feedback.itemId === undefined && <Feedback kind={feedback.kind}>{feedback.message}</Feedback>}
        {creating && (
          <MatterItemForm fields={fields} itemName={itemName} saving={busy} errors={feedback?.errors}
            onSave={(values) => void mutate(path, jsonRequest("POST", values), "Added successfully.", `Couldn’t add the ${itemName}. Try again.`)}
            onCancel={() => { setFeedback(null); finishInteraction(); }} />
        )}
      </div>
      {!!data?.length && (
        <ul className="divide-y divide-stone-200 border-t border-stone-200">
          {data.map((item) => {
            const itemPath = `${path}/${item.id}` as const;
            const active = !readOnly && interaction && "itemId" in interaction && interaction.itemId === item.id;
            const editing = active && interaction.type === "edit";
            const deleting = active && interaction.type === "delete";
            const itemFeedback = feedback?.itemId === item.id ? feedback : null;
            const transition = "status" in item && (item.status === "Pending" ? "complete" : collection === "tasks" ? "reopen" : null);
            return (
              <li key={item.id} className="space-y-3 px-4 py-4 sm:px-6">
                {itemFeedback && <Feedback kind={itemFeedback.kind}>{itemFeedback.message}</Feedback>}
                {editing ? (
                  <MatterItemForm fields={fields} itemName={itemName} item={item} saving={busy} errors={itemFeedback?.errors}
                    onSave={(values) => void mutate(itemPath, jsonRequest("PUT", values), "Changes saved.", `Couldn’t save the ${itemName}. Try again.`, item.id)}
                    onCancel={() => { setFeedback(null); finishInteraction(item.id); }} />
                ) : (
                  <div className="flex flex-col gap-3 lg:flex-row lg:items-start lg:justify-between">
                    <MatterItemDetails item={item} />
                    {!readOnly && !deleting && (
                      <div className="flex shrink-0 flex-wrap gap-2">
                        {transition && (
                          <button type="button" className="admin-secondary" disabled={blocked}
                            onClick={() => void mutate(`${itemPath}/${transition}`, { method: "POST" },
                              transition === "complete" ? "Marked complete." : "Task reopened.",
                              `Couldn’t ${transition} the ${itemName}. Try again.`, item.id)}>
                            {transition === "complete" ? `Complete ${itemName}` : "Reopen task"}
                          </button>
                        )}
                        <button id={editId(item.id)} type="button" className="admin-secondary" disabled={blocked}
                          onClick={() => beginInteraction({ type: "edit", itemId: item.id })}>Edit {itemName}</button>
                        <button type="button" className="admin-secondary text-red-800" disabled={blocked}
                          onClick={() => beginInteraction({ type: "delete", itemId: item.id })}>Delete {itemName}</button>
                      </div>
                    )}
                  </div>
                )}
                {deleting && (
                  <div className="space-y-3 rounded-lg bg-stone-50 p-4">
                    <p className="text-sm font-medium">Permanently delete this {itemName}?</p>
                    <div className="flex flex-wrap gap-2">
                      <button type="button" className="admin-primary" disabled={busy}
                        onClick={() => void mutate(itemPath, { method: "DELETE" }, "Deleted successfully.",
                          `Couldn’t delete the ${itemName}. Try again.`, item.id)}>
                        {busy ? "Deleting…" : "Confirm delete"}
                      </button>
                      <button type="button" className="admin-secondary" autoFocus disabled={busy}
                        onClick={() => { setFeedback(null); finishInteraction(item.id); }}>Cancel delete</button>
                    </div>
                  </div>
                )}
              </li>
            );
          })}
        </ul>
      )}
    </section>
  );
}

function MatterItemDetails({ item }: { item: MatterItem }) {
  if ("text" in item) {
    return (
      <div className="min-w-0 space-y-2">
        <p className="whitespace-pre-wrap wrap-anywhere">{item.text}</p>
        <p className="text-sm text-neutral-600">
          Added <MatterTimestamp value={item.createdAtUtc} />
          {item.updatedAtUtc !== item.createdAtUtc && <> · Edited <MatterTimestamp value={item.updatedAtUtc} /></>}
        </p>
      </div>
    );
  }

  const date = "dueDate" in item ? item.dueDate : item.date;
  const status = item.isOverdue ? "Overdue" : item.status;
  const statusColor = item.isOverdue ? "bg-red-50 text-red-800" : "bg-stone-100 text-neutral-800";
  return (
    <div className="min-w-0 space-y-2">
      <div className="flex flex-wrap items-baseline gap-x-3 gap-y-1">
        <h3 className="min-w-0 font-semibold wrap-anywhere">{item.title}</h3>
        <span className={`rounded-md px-2 py-1 text-xs font-semibold ${statusColor}`}>{status}</span>
      </div>
      <p className="text-sm text-neutral-600">
        {date ? <>Due <time dateTime={date}>{formatCalendarDate(date)}</time></> : "No due date"}
      </p>
      {"description" in item && item.description && (
        <p className="whitespace-pre-wrap text-sm text-neutral-700 wrap-anywhere">{item.description}</p>
      )}
    </div>
  );
}
