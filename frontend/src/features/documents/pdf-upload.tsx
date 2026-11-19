import { useId } from "react";
import { Field } from "@/components/workspace-ui";

const maximumPdfBytes = 20 * 1024 * 1024;
export const pdfSizeError = "Choose a PDF of 20 MiB or less.";
export const invalidPdfError = "Choose a valid, readable PDF with a filename up to 255 characters.";

export function validatePdfFile(file: File | null) {
  if (!file?.size) return "Choose a nonempty PDF file.";
  if (file.size > maximumPdfBytes) return pdfSizeError;
}

export function PdfFileField({ error, autoFocus, onChange }: {
  error?: string;
  autoFocus?: boolean;
  onChange: (file: File | null) => void;
}) {
  const id = useId();
  const helpId = `${id}-help`;
  return (
    <Field id={id} label="PDF file" error={error}>
      <p id={helpId} className="mt-1 text-sm text-neutral-600">Maximum file size 20 MiB.</p>
      <input id={id} className="admin-input text-sm file:mr-3 file:font-semibold" type="file"
        accept=".pdf,application/pdf" autoFocus={autoFocus} required
        aria-invalid={error ? true : undefined} aria-describedby={error ? `${helpId} ${id}-error` : helpId}
        onChange={(event) => onChange(event.target.files?.[0] ?? null)} />
    </Field>
  );
}
