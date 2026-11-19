import { StatusPill } from "@/components/status-pill";
import Link from "next/link";
import { useId, useState, type FormEvent, type ReactNode } from "react";
import { Field, Timestamp, fieldDescription } from "@/components/workspace-ui";
import { documentApiPath, documentHref } from "@/features/documents/document-types";
import { PdfPreview } from "@/features/documents/pdf-preview";
import type { DocumentSubmission, SubmissionDecision } from "./request-types";

export function SubmissionHistory({ matterId, submissions, canReview, busy, feedback, onReview }: {
  matterId: number;
  submissions: DocumentSubmission[];
  canReview: boolean;
  busy: boolean;
  feedback: ReactNode;
  onReview: (versionId: number, decision: SubmissionDecision, explanation?: string) => Promise<boolean>;
}) {
  const [preview, setPreview] = useState<number | null>(null);
  const [correction, setCorrection] = useState<number | null>(null);
  const [explanation, setExplanation] = useState("");
  const [error, setError] = useState("");
  const inputId = useId();

  async function submitCorrection(event: FormEvent<HTMLFormElement>, versionId: number) {
    event.preventDefault();
    const text = explanation.trim();
    if (!text) {
      setError("Explain what the client should correct.");
      return;
    }
    if (await onReview(versionId, "CorrectionsRequested", text)) setCorrection(null);
  }

  return (
    <section className="admin-panel" aria-labelledby="submissions-heading">
      <div className="space-y-3 p-4 sm:px-6 sm:py-5">
        <h2 id="submissions-heading" className="text-xl font-semibold tracking-tight">Submission history</h2>
        {feedback}
        {!submissions.length && <p className="text-sm text-neutral-600">No PDFs submitted yet.</p>}
      </div>
      {!!submissions.length && (
        <ol className="divide-y divide-stone-200 border-t border-stone-200">
          {submissions.map((submission, index) => {
            const { documentVersionId, versionNumber, documentId, fileName } = submission;
            const open = preview === documentVersionId;
            const reviewing = canReview && index === 0 && !submission.decision;
            const correcting = reviewing && correction === documentVersionId;
            const status = submission.decision === "Accepted" ? "Accepted"
              : submission.decision === "CorrectionsRequested" ? "Corrections requested" : "Awaiting review";
            return (
              <li key={documentVersionId} className="space-y-3 px-4 py-4 sm:px-6">
                <div className="flex flex-wrap items-start justify-between gap-3">
                  <div className="min-w-0 space-y-1 wrap-anywhere">
                    <h3 className="font-semibold">Version {versionNumber} · {fileName}</h3>
                    <p className="text-sm text-neutral-600">Submitted <Timestamp value={submission.submittedAtUtc} /></p>
                    <p className="text-sm text-neutral-700">
                      <StatusPill value={status} />
                      {submission.reviewedAtUtc && <> · <Timestamp value={submission.reviewedAtUtc} /></>}
                    </p>
                  </div>
                  <div className="flex flex-wrap gap-2">
                    <button type="button" className="admin-secondary" aria-expanded={open} aria-controls={`submission-preview-${documentVersionId}`}
                      aria-label={`${open ? "Close preview for" : "View PDF for"} version ${versionNumber}`}
                      onClick={() => setPreview(open ? null : documentVersionId)}>{open ? "Close preview" : "View PDF"}</button>
                    {submission.decision === "Accepted" && <Link href={documentHref(matterId, documentId, true)} className="admin-secondary">View document</Link>}
                  </div>
                </div>
                {submission.correctionExplanation && (
                  <p className="whitespace-pre-wrap rounded-lg bg-stone-50 p-3 text-sm text-neutral-800 wrap-anywhere">{submission.correctionExplanation}</p>
                )}
                <div id={`submission-preview-${documentVersionId}`} hidden={!open} className="rounded-lg bg-stone-50 p-3 sm:p-4">
                  {open && <PdfPreview path={`${documentApiPath(matterId, documentId)}/versions/${versionNumber}/file`}
                    version={{ versionNumber, originalFileName: fileName }} />}
                </div>
                {reviewing && !correcting && (
                  <div className="flex flex-wrap gap-2">
                    <button className="admin-primary" type="button" disabled={busy}
                      onClick={() => void onReview(documentVersionId, "Accepted")}>Accept submission</button>
                    <button id={`request-corrections-${documentVersionId}`} className="admin-secondary" type="button" disabled={busy}
                      onClick={() => { setCorrection(documentVersionId); setExplanation(""); setError(""); }}>Request corrections</button>
                  </div>
                )}
                {correcting && (
                  <form method="post" className="rounded-lg bg-stone-50 p-4" onSubmit={(event) => void submitCorrection(event, documentVersionId)}>
                    <fieldset disabled={busy} className="min-w-0 space-y-3">
                      <Field id={inputId} label="Correction explanation" error={error || undefined}>
                        <textarea id={inputId} className="admin-input" rows={4} required maxLength={5000} autoFocus
                          {...fieldDescription(inputId, error || undefined)}
                          value={explanation} onChange={(event) => { setExplanation(event.target.value); setError(""); }} />
                      </Field>
                      <div className="flex flex-wrap gap-2">
                        <button type="submit" className="admin-primary">Send correction request</button>
                        <button type="button" className="admin-secondary" onClick={() => {
                          setCorrection(null);
                          requestAnimationFrame(() => document.getElementById(`request-corrections-${documentVersionId}`)?.focus());
                        }}>Cancel</button>
                      </div>
                    </fieldset>
                  </form>
                )}
              </li>
            );
          })}
        </ol>
      )}
    </section>
  );
}
