import { StatusPill } from "@/components/status-pill";
import { useRef, useState, type ReactNode } from "react";
import { Timestamp } from "@/components/workspace-ui";
import { templateLabels, type EmailTemplate, type ExternalRequest } from "./request-types";

export function RequestDelivery({ request, canResend, canRevoke, busy, feedback, onResend, onRevoke }: {
  request: ExternalRequest;
  canResend: boolean;
  canRevoke: boolean;
  busy: boolean;
  feedback: ReactNode;
  onResend: (template?: EmailTemplate) => void;
  onRevoke: () => Promise<boolean>;
}) {
  const [confirming, setConfirming] = useState(false);
  const revokeButton = useRef<HTMLButtonElement>(null);
  const correction = request.uploadStatus === "CorrectionsRequested";
  const completedSigning = request.action === "Sign" && request.progress === "Signed";
  const resendTemplate = completedSigning ? "Reminder" : correction ? "Correction" : undefined;
  const resendLabel = completedSigning ? "Resend signed PDF link" : correction ? "Resend correction" : "Resend invitation";

  return (
    <section className="admin-panel" aria-labelledby="delivery-heading">
      <div className="space-y-3 p-4 sm:px-6 sm:py-5">
        <h2 id="delivery-heading" className="text-xl font-semibold tracking-tight">Email delivery</h2>
        {feedback}
        {canResend && <p className="text-sm text-neutral-600">Sending another email issues a new link and invalidates the previous one.</p>}
        {(canResend || (canRevoke && !confirming)) && (
          <div className="flex flex-wrap gap-2">
            {canResend && <>
              <button type="button" className="admin-secondary" disabled={busy || confirming}
                onClick={() => onResend(resendTemplate)}>{resendLabel}</button>
              {!completedSigning && (
                <button type="button" className="admin-secondary" disabled={busy || confirming}
                  onClick={() => onResend("Reminder")}>Send reminder</button>
              )}
            </>}
            {canRevoke && !confirming && <button ref={revokeButton} type="button" className="admin-secondary text-red-800" disabled={busy}
              onClick={() => setConfirming(true)}>Revoke link</button>}
          </div>
        )}
        {canRevoke && confirming && (
          <div className="space-y-3 rounded-lg bg-stone-50 p-4">
            <p className="text-sm font-medium">Revoke the current link?</p>
            <div className="flex flex-wrap gap-2">
              <button type="button" className="admin-primary" disabled={busy} onClick={async () => {
                if (await onRevoke()) setConfirming(false);
              }}>Confirm revocation</button>
              <button type="button" className="admin-secondary" disabled={busy} autoFocus onClick={() => {
                setConfirming(false);
                requestAnimationFrame(() => revokeButton.current?.focus());
              }}>Cancel revocation</button>
            </div>
          </div>
        )}
        {!request.deliveryAttempts.length && <p className="text-sm text-neutral-600">No email attempts recorded.</p>}
      </div>
      {!!request.deliveryAttempts.length && (
        <ol aria-label="Delivery attempts, newest first" className="divide-y divide-stone-200 border-t border-stone-200">
          {request.deliveryAttempts.map((attempt) => (
            <li key={attempt.id} className="grid gap-1 px-4 py-3 text-sm sm:grid-cols-[minmax(0,1fr)_minmax(0,1fr)_auto] sm:gap-4 sm:px-6">
              <div className="font-semibold">{templateLabels[attempt.template]}</div>
              <div className={attempt.status === "Failed" ? "text-red-800" : "text-neutral-700"}>
                <StatusPill value={attempt.status} />
                {attempt.failureCode && <span> · {attempt.failureCode === "SmtpTimeout" ? "SMTP timed out" : "SMTP send failed"}</span>}
              </div>
              <p className="text-neutral-600"><Timestamp value={attempt.finishedAtUtc ?? attempt.startedAtUtc} /></p>
            </li>
          ))}
        </ol>
      )}
    </section>
  );
}
