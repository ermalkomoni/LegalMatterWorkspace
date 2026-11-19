import { useId, useState, type FormEvent } from "react";
import { Feedback, Field, fieldDescription } from "@/components/workspace-ui";
import { useAdminSession } from "@/features/auth/admin-session";
import { ApiError, jsonRequest, type FieldErrors } from "@/lib/api";
import { documentApiPath, type DocumentVersion, type PdfDocument } from "./document-types";

type DocumentFormProps = {
  onCancel: () => void;
  onConflict: () => void;
} & (
  | { mode: "new"; matterId: number; onSaved: (version: DocumentVersion) => void }
  | { mode: "version"; document: PdfDocument; onSaved: (version: DocumentVersion) => void }
  | { mode: "metadata"; document: PdfDocument; onSaved: (document: PdfDocument) => void }
);

const maximumPdfBytes = 20 * 1024 * 1024;
const sizeError = "Choose a PDF of 20 MiB or less.";
const formLabels = {
  new: { heading: "Upload a PDF", submit: "Upload PDF" },
  version: { heading: "Upload a new version", submit: "Upload version" },
  metadata: { heading: "Edit document details", submit: "Save changes" },
};

export function DocumentForm(props: DocumentFormProps) {
  const { mode, onCancel, onConflict } = props;
  const { request } = useAdminSession();
  const { heading, submit: submitLabel } = formLabels[mode];
  const id = useId();
  const helpId = `${id}-file-help`;
  const [title, setTitle] = useState(mode === "metadata" ? props.document.title : "");
  const [category, setCategory] = useState(mode === "metadata" ? props.document.category ?? "" : "");
  const [file, setFile] = useState<File | null>(null);
  const [errors, setErrors] = useState<FieldErrors>({});
  const [feedback, setFeedback] = useState("");
  const [saving, setSaving] = useState(false);

  async function submit(event: FormEvent<HTMLFormElement>) {
    event.preventDefault();
    if (saving) return;
    setFeedback("");
    const fields: FieldErrors = {};
    if (mode !== "version" && !title.trim()) fields.title = ["Enter a title."];
    if (mode !== "metadata") {
      if (!file?.size) fields.file = ["Choose a nonempty PDF file."];
      else if (file.size > maximumPdfBytes) fields.file = [sizeError];
    }
    setErrors(fields);
    if (Object.keys(fields).length) return;

    setSaving(true);
    try {
      if (props.mode === "metadata") {
        const { matterId, id: documentId } = props.document;
        const details = { title: title.trim(), category: category.trim() || null };
        props.onSaved(await request<PdfDocument>(documentApiPath(matterId, documentId), jsonRequest("PUT", details)));
      } else if (file) {
        const body = new FormData();
        body.set("file", file);
        if (props.mode === "new") {
          body.set("title", title.trim());
          if (category.trim()) body.set("category", category.trim());
          props.onSaved(await request<DocumentVersion>(`/matters/${props.matterId}/documents`, { method: "POST", body }));
        } else {
          const { matterId, id: documentId } = props.document;
          props.onSaved(await request<DocumentVersion>(`${documentApiPath(matterId, documentId)}/versions`, { method: "POST", body }));
        }
      }
    } catch (error) {
      if (error instanceof ApiError && error.status === 413) {
        setErrors({ file: [sizeError] });
      } else if (error instanceof ApiError && error.status === 400) {
        if (Object.keys(error.fieldErrors).length) setErrors(error.fieldErrors);
        else if (mode !== "metadata") setErrors({ file: ["Choose a valid, readable PDF with a filename up to 255 characters."] });
        else setFeedback("Check the document details and try again.");
      } else if (error instanceof ApiError && error.status === 409) {
        setFeedback(mode === "version"
          ? "The version could not be uploaded. Review the refreshed details. An active signing workflow can block uploads."
          : "The matter or document changed. Review the refreshed details and try again.");
        onConflict();
      } else {
        setFeedback(mode === "metadata" ? "Couldn’t save the document details. Try again." : "Couldn’t upload the PDF. Try again.");
      }
    } finally {
      setSaving(false);
    }
  }

  const fileError = errors.file?.[0];
  return (
    <form method="post" onSubmit={submit} className="admin-panel p-4 sm:p-5">
      <fieldset disabled={saving} className="min-w-0 space-y-4">
        <legend className="mb-3 text-lg font-semibold">{heading}</legend>
        {mode !== "version" && (
          <div className="grid gap-4 sm:grid-cols-2">
            <Field id={`${id}-title`} label="Title" error={errors.title?.[0]}>
              <input id={`${id}-title`} className="admin-input" maxLength={200} required autoFocus
                value={title} onChange={(event) => setTitle(event.target.value)}
                {...fieldDescription(`${id}-title`, errors.title?.[0])} />
            </Field>
            <Field id={`${id}-category`} label="Category (optional)" error={errors.category?.[0]}>
              <input id={`${id}-category`} className="admin-input" maxLength={100}
                value={category} onChange={(event) => setCategory(event.target.value)}
                {...fieldDescription(`${id}-category`, errors.category?.[0])} />
            </Field>
          </div>
        )}
        {mode !== "metadata" && (
          <Field id={`${id}-file`} label="PDF file" error={fileError}>
            <p id={helpId} className="mt-1 text-sm text-neutral-600">Maximum file size 20 MiB.</p>
            <input id={`${id}-file`} className="admin-input text-sm file:mr-3 file:font-semibold" type="file"
              accept=".pdf,application/pdf" autoFocus={mode === "version"} required
              aria-invalid={fileError ? true : undefined}
              aria-describedby={fileError ? `${helpId} ${id}-file-error` : helpId}
              onChange={(event) => {
                setFile(event.target.files?.[0] ?? null);
                setErrors((current) => ({ ...current, file: [] }));
              }} />
          </Field>
        )}
        {feedback && <Feedback kind="error">{feedback}</Feedback>}
        <div className="flex flex-wrap gap-2">
          <button type="submit" className="admin-primary">
            {saving ? (mode === "metadata" ? "Saving…" : "Uploading…") : submitLabel}
          </button>
          <button type="button" className="admin-secondary" onClick={onCancel}>Cancel</button>
        </div>
      </fieldset>
    </form>
  );
}
