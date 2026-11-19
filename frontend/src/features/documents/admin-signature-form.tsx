import { useId, useState, type FormEvent } from "react";
import { Feedback, Field, fieldDescription } from "@/components/workspace-ui";
import { useAdminSession } from "@/features/auth/admin-session";
import { ApiError, jsonRequest, type FieldErrors } from "@/lib/api";
import { documentApiPath, type DocumentVersion, type PdfDocument } from "./document-types";
import { signingConsentText, type Signature } from "./signing-types";

export function AdminSignatureForm({ document, version, canSubmit, saving, onSaving, onSigned, onRefresh }: {
  document: PdfDocument;
  version: DocumentVersion;
  canSubmit: boolean;
  saving: boolean;
  onSaving: (saving: boolean) => void;
  onSigned: (signature: Signature) => void;
  onRefresh: () => void;
}) {
  const { request } = useAdminSession();
  const id = useId();
  const [typedName, setTypedName] = useState("");
  const [consentAccepted, setConsentAccepted] = useState(false);
  const [errors, setErrors] = useState<FieldErrors>({});
  const [feedback, setFeedback] = useState("");

  async function submit(event: FormEvent<HTMLFormElement>) {
    event.preventDefault();
    if (saving || !canSubmit) return;
    setFeedback("");
    const name = typedName.trim();
    const fields: FieldErrors = {};
    if (!name) fields.typedname = ["Enter your name."];
    else if (/\p{Cc}/u.test(name)) fields.typedname = ["Enter a name without control characters."];
    if (!consentAccepted) fields.consentaccepted = ["Confirm your consent to sign this version."];
    setErrors(fields);
    if (Object.keys(fields).length) return;

    onSaving(true);
    try {
      const path = `${documentApiPath(document.matterId, document.id)}/sign` as const;
      const signature = await request<Signature>(path, jsonRequest("POST", {
        sourceVersionId: version.id, typedName: name, consentAccepted,
      }));
      onSigned(signature);
    } catch (error) {
      if (error instanceof ApiError && error.status === 400) {
        setErrors(error.fieldErrors);
        if (!error.fieldErrors.typedname?.length && !error.fieldErrors.consentaccepted?.length) {
          setFeedback("Couldn’t sign this PDF. Check the name and consent and try again.");
        }
      } else {
        setFeedback(error instanceof ApiError && error.status === 409
          ? "The signing status changed. Check the refreshed document before trying again."
          : "Couldn’t confirm signing. Check the refreshed document before trying again.");
        onRefresh();
      }
    } finally {
      onSaving(false);
    }
  }

  return (
    <form method="post" onSubmit={submit} className="mt-4 rounded-lg border border-stone-200 bg-white p-4 sm:p-5">
      <fieldset disabled={saving || !canSubmit} className="min-w-0 space-y-4">
        <legend className="mb-3 font-semibold">Sign version {version.versionNumber} as Admin</legend>
        <Field id={`${id}-name`} label="Typed name" error={errors.typedname?.[0]}>
          <input id={`${id}-name`} required maxLength={200} autoComplete="name" className="admin-input sm:max-w-sm"
            value={typedName} onChange={(event) => {
              setTypedName(event.target.value);
              setErrors((previous) => ({ ...previous, typedname: [] }));
            }} {...fieldDescription(`${id}-name`, errors.typedname?.[0])} />
        </Field>
        <div>
          <label className="flex items-start gap-3 text-sm" htmlFor={`${id}-consent`}>
            <input id={`${id}-consent`} type="checkbox" required checked={consentAccepted} className="mt-0.5 size-4 shrink-0 accent-red-800"
              {...fieldDescription(`${id}-consent`, errors.consentaccepted?.[0])} onChange={(event) => {
                setConsentAccepted(event.target.checked);
                setErrors((previous) => ({ ...previous, consentaccepted: [] }));
              }} />
            <span>{signingConsentText}</span>
          </label>
          {errors.consentaccepted?.[0] && <p role="alert" id={`${id}-consent-error`} className="mt-1.5 text-sm text-red-800">
            {errors.consentaccepted[0]}
          </p>}
        </div>
        <button type="submit" className="admin-primary">{saving ? "Signing…" : "Sign PDF"}</button>
      </fieldset>
      {saving && <p role="status" className="mt-3 text-sm text-neutral-600">Creating the signed PDF…</p>}
      {feedback && <div className="mt-3"><Feedback kind="error">{feedback}</Feedback></div>}
    </form>
  );
}
