import { useId, useState, type ChangeEvent, type FormEvent } from "react";
import { Feedback, Field, fieldDescription } from "@/components/workspace-ui";
import { ApiError, type FieldErrors } from "@/lib/api";
import type { ExternalRequest } from "./request-types";

const fields = [
  { name: "title", label: "Request title", type: "text", maxLength: 200, required: true },
  { name: "recipientName", label: "Recipient name", type: "text", maxLength: 200, required: true },
  { name: "recipientEmail", label: "Recipient email", type: "email", maxLength: 254, required: true },
  { name: "expiresAtUtc", label: "Expiry (local time, optional)", type: "datetime-local" },
  { name: "instructions", label: "Instructions (optional)", type: "textarea", maxLength: 5000 },
] as const;

type RequestDraft = Record<(typeof fields)[number]["name"], string>;
type RequestInput = Omit<RequestDraft, "expiresAtUtc"> & { expiresAtUtc: string | null };
type SigningDocument = { title: string; originalFileName: string; versionNumber: number };

export function RequestForm({ submitRequest, onCreated, onCancel, onRefresh, signingDocument, refreshing = false }: {
  submitRequest: (values: RequestInput) => Promise<ExternalRequest>;
  onCreated: (request: ExternalRequest) => void;
  onCancel: () => void;
  onRefresh: () => void;
  signingDocument?: SigningDocument;
  refreshing?: boolean;
}) {
  const id = useId();
  const [draft, setDraft] = useState<RequestDraft>({
    title: signingDocument?.title ?? "", recipientName: "", recipientEmail: "", instructions: "", expiresAtUtc: "",
  });
  const [errors, setErrors] = useState<FieldErrors>({});
  const [feedback, setFeedback] = useState("");
  const [saving, setSaving] = useState(false);

  async function submit(event: FormEvent<HTMLFormElement>) {
    event.preventDefault();
    if (saving || refreshing) return;
    setFeedback("");
    const validation: FieldErrors = {};
    if (!draft.title.trim()) validation.title = ["Enter a request title."];
    if (!signingDocument && !draft.recipientName.trim()) validation.recipientname = ["Enter the recipient’s name."];
    const expiry = draft.expiresAtUtc ? new Date(draft.expiresAtUtc) : null;
    const now = Date.now();
    if (expiry && (!Number.isFinite(expiry.getTime()) || expiry.getTime() <= now || expiry.getTime() > now + 365 * 86400000)) {
      validation.expiresatutc = ["Choose a future expiry within one year."];
    }
    setErrors(validation);
    if (Object.keys(validation).length) return;

    setSaving(true);
    try {
      onCreated(await submitRequest({
        title: draft.title.trim(), recipientName: draft.recipientName.trim(), recipientEmail: draft.recipientEmail.trim(),
        instructions: draft.instructions.trim(), expiresAtUtc: expiry?.toISOString() ?? null,
      }));
    } catch (error) {
      if (error instanceof ApiError && error.status === 400) {
        setErrors(error.fieldErrors);
        setFeedback(error.fieldErrors.request?.[0] ?? "Check the request details and try again.");
      } else if (error instanceof ApiError && error.status === 409) {
        setFeedback(signingDocument
          ? "The signing status changed. Check the document and its requests before trying again."
          : "The matter changed. Check its current status before trying again.");
        onRefresh();
      } else {
        setFeedback("Couldn’t confirm the request. Check the refreshed list before sending again.");
        onRefresh();
      }
    } finally {
      setSaving(false);
    }
  }

  return (
    <form method="post" className="admin-panel p-4 sm:p-5" onSubmit={submit}>
      <fieldset disabled={saving || refreshing} className="min-w-0 space-y-4">
        <legend className="mb-3 text-lg font-semibold">{signingDocument ? "New signature request" : "New document request"}</legend>
        {signingDocument && (
          <p className="text-sm text-neutral-700 wrap-anywhere">
            {signingDocument.title} · Version {signingDocument.versionNumber} · {signingDocument.originalFileName}
          </p>
        )}
        <div className="grid gap-4 sm:grid-cols-2">
          {fields.map((field) => {
            const fieldId = `${id}-${field.name}`;
            const key = field.name.toLowerCase();
            const error = errors[key]?.[0];
            const optionalSignerName = signingDocument && field.name === "recipientName";
            const input = {
              id: fieldId, className: "admin-input", value: draft[field.name],
              ...fieldDescription(fieldId, error),
              onChange: (event: ChangeEvent<HTMLInputElement | HTMLTextAreaElement>) => {
                const value = event.target.value;
                setDraft((previous) => ({ ...previous, [field.name]: value }));
                setErrors((previous) => ({ ...previous, [key]: [] }));
              },
            };
            return (
              <div key={field.name} className={field.type === "textarea" ? "sm:col-span-2" : "min-w-0"}>
                <Field id={fieldId} label={optionalSignerName ? "Recipient name (optional)" : field.label} error={error}>
                  {field.type === "textarea" ? <textarea {...input} maxLength={field.maxLength} rows={4} /> : (
                    <input {...input} type={field.type} autoFocus={field.name === "title"}
                      required={!optionalSignerName && "required" in field && field.required}
                      maxLength={"maxLength" in field ? field.maxLength : undefined} />
                  )}
                  {field.name === "expiresAtUtc" && <p className="mt-1 text-sm text-neutral-600">Leave blank to use the default expiry.</p>}
                </Field>
              </div>
            );
          })}
        </div>
        {feedback && <Feedback kind="error">{feedback}</Feedback>}
        <div className="flex flex-wrap gap-2">
          <button type="submit" className="admin-primary">{saving ? "Sending…" : "Send invitation"}</button>
          <button type="button" className="admin-secondary" onClick={onCancel}>Cancel</button>
        </div>
      </fieldset>
    </form>
  );
}
