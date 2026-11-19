"use client";

import { useEffect, useRef, useState, type FormEvent } from "react";
import { Feedback, Field, fieldDescription, Timestamp } from "@/components/workspace-ui";
import { ApiError, jsonRequest, type FieldErrors } from "@/lib/api";
import type { SigningProgress } from "./request-types";
import { pdfWithLink, requestWithLink, useLinkToken } from "./use-link-token";

type SignPath = `/external/sign/${string}`;
type SignContext = {
  title: string;
  recipientName: string;
  instructions: string;
  progress: SigningProgress;
  expiresAtUtc: string;
  canSubmit: boolean;
  canReadPdf: boolean;
  consentText: string | null;
  signedAtUtc: string | null;
};
type LoadState =
  | { status: "loading" }
  | { status: "unavailable" }
  | { status: "error"; message: string }
  | { status: "ready"; context: SignContext; source: Blob | null };
type Action = "viewed" | "started" | "sign" | "download";

function errorMessage(error: unknown, fallback: string) {
  return error instanceof ApiError && error.status === 429
    ? "Too many requests. Wait a minute and try again."
    : fallback;
}

function UnavailableSign() {
  return (
    <section className="admin-panel space-y-3 p-5 sm:p-7" role="alert">
      <h1 className="text-2xl font-semibold tracking-tight">Signing link unavailable</h1>
      <p className="text-sm leading-6 text-neutral-700">Open the most recent link you received or contact the sender for help.</p>
    </section>
  );
}

export function PublicSignPage({ requestId }: { requestId: string | null }) {
  const link = useLinkToken();
  if (!requestId || (link && !link.token)) return <UnavailableSign />;
  if (!link?.token) return <p role="status">Loading signing request…</p>;
  return <PublicSign key={link.revision} path={`/external/sign/${requestId}`} token={link.token} />;
}

function PublicSign({ path, token }: { path: SignPath; token: string }) {
  const [state, setState] = useState<LoadState>({ status: "loading" });
  const [attempt, setAttempt] = useState(0);

  useEffect(() => {
    const controller = new AbortController();
    const { signal } = controller;

    async function load() {
      try {
        const context = await requestWithLink<SignContext>(path, token, { signal });
        const source = context.progress !== "Signed" && context.canReadPdf
          ? await pdfWithLink(`${path}/file`, token, { signal }) : null;
        if (!signal.aborted) setState({ status: "ready", context, source });
      } catch (error) {
        if (signal.aborted) return;
        if (error instanceof ApiError && error.status === 404) setState({ status: "unavailable" });
        else setState({ status: "error", message: errorMessage(error, "Couldn’t load the signing request or its PDF. Try again.") });
      }
    }

    queueMicrotask(() => { if (!signal.aborted) void load(); });
    return () => controller.abort();
  }, [path, token, attempt]);

  function reload() {
    setState({ status: "loading" });
    setAttempt((value) => value + 1);
  }

  if (state.status === "unavailable") return <UnavailableSign />;
  if (state.status === "loading") return <p role="status">Loading signing request…</p>;
  if (state.status === "error") return (
    <section className="admin-panel space-y-3 p-5 sm:p-7">
      <Feedback kind="error">{state.message}</Feedback>
      <button className="admin-secondary" type="button" onClick={reload}>Try again</button>
    </section>
  );

  return <SigningRequest path={path} token={token} initialContext={state.context} source={state.source}
    reload={reload} onUnavailable={() => setState({ status: "unavailable" })} />;
}

function SigningRequest({ path, token, initialContext, source, reload, onUnavailable }: {
  path: SignPath;
  token: string;
  initialContext: SignContext;
  source: Blob | null;
  reload: () => void;
  onUnavailable: () => void;
}) {
  const [context, setContext] = useState(initialContext);
  const [sourceDisplayed, setSourceDisplayed] = useState(false);
  const [busy, setBusy] = useState<Action | null>(null);
  const [feedback, setFeedback] = useState("");
  const [checkStatus, setCheckStatus] = useState(false);
  const [fieldErrors, setFieldErrors] = useState<FieldErrors>({});
  const operation = useRef<AbortController | null>(null);

  useEffect(() => () => operation.current?.abort(), []);

  async function run(action: Action, work: (signal: AbortSignal) => Promise<void>, fallback: string) {
    if (operation.current) return;
    const controller = new AbortController();
    const { signal } = controller;
    operation.current = controller;
    setBusy(action);
    setFeedback("");
    setCheckStatus(false);
    try {
      await work(signal);
    } catch (error) {
      if (signal.aborted) return;
      if (error instanceof ApiError && error.status === 404) onUnavailable();
      else if (error instanceof ApiError && error.status === 409) reload();
      else if (action === "sign" && error instanceof ApiError && error.status === 400) {
        setFieldErrors(error.fieldErrors);
        if (!error.fieldErrors.typedname?.length && !error.fieldErrors.consentaccepted?.length) {
          setFeedback("Couldn’t sign the PDF. Check the name and consent and try again.");
        }
      } else {
        setFeedback(errorMessage(error, fallback));
        setCheckStatus(action === "sign" && !(error instanceof ApiError && error.status === 429));
      }
    } finally {
      operation.current = null;
      if (!signal.aborted) setBusy(null);
    }
  }

  function advance(action: "viewed" | "started") {
    void run(action, async (signal) => {
      const next = await requestWithLink<SignContext>(`${path}/${action}`, token, { method: "POST", signal });
      if (!signal.aborted) setContext(next);
    }, action === "viewed" ? "Couldn’t confirm that you viewed the PDF. Try again." : "Couldn’t start signing. Try again.");
  }

  function onSourceDisplayed() {
    setSourceDisplayed(true);
    if (context.progress === "Sent" && context.canSubmit) advance("viewed");
  }

  function sign(event: FormEvent<HTMLFormElement>) {
    event.preventDefault();
    if (!sourceDisplayed || context.progress !== "Started" || !context.canSubmit || !context.consentText) return;
    const values = new FormData(event.currentTarget);
    const typedName = String(values.get("typedName") ?? "").trim();
    const consentAccepted = values.get("consentAccepted") === "on";
    const errors: FieldErrors = {};
    if (!typedName) errors.typedname = ["Enter your name."];
    if (!consentAccepted) errors.consentaccepted = ["Check the box to give your consent."];
    setFieldErrors(errors);
    if (!typedName || !consentAccepted) return;

    void run("sign", async (signal) => {
      const result = await requestWithLink<{ signedAtUtc: string }>(path, token,
        { ...jsonRequest("POST", { typedName, consentAccepted }), signal });
      if (!signal.aborted) setContext({ ...context, progress: "Signed", canSubmit: false, signedAtUtc: result.signedAtUtc });
    }, "Couldn’t confirm the signature. Check the request status before trying again.");
  }

  function download() {
    void run("download", async (signal) => {
      const pdf = await pdfWithLink(`${path}/file?download=true`, token, { signal });
      if (signal.aborted) return;
      const link = document.createElement("a");
      link.href = URL.createObjectURL(pdf);
      link.download = "signed-document.pdf";
      link.click();
      window.setTimeout(() => URL.revokeObjectURL(link.href), 1000);
    }, "Couldn’t download the signed PDF. Try again.");
  }

  const consentError = fieldErrors.consentaccepted?.[0];
  return (
    <section className="admin-panel space-y-5 p-5 sm:p-7">
      <div className="space-y-2">
        <p className="text-sm font-medium text-neutral-600">Signature request</p>
        <h1 className="text-3xl font-semibold tracking-tight wrap-anywhere">{context.title}</h1>
        {context.recipientName && <p className="text-sm text-neutral-700 wrap-anywhere">Requested from {context.recipientName}</p>}
      </div>
      {feedback && <Feedback kind="error">{feedback}</Feedback>}
      {checkStatus && <button className="admin-secondary" type="button" onClick={reload}>Check request status</button>}
      {context.progress === "Signed" ? (
        <>
          <Feedback>Document signed{context.signedAtUtc ? <> on <Timestamp value={context.signedAtUtc} /></> : ""}.</Feedback>
          {context.canReadPdf && <button type="button" className="admin-primary" disabled={!!busy} onClick={download}>
            {busy === "download" ? "Downloading…" : "Download signed PDF"}
          </button>}
        </>
      ) : (
        <>
          {context.instructions && <p className="whitespace-pre-wrap text-sm leading-6 text-neutral-800 wrap-anywhere">{context.instructions}</p>}
          <p className="text-sm text-neutral-600">Link expires <Timestamp value={context.expiresAtUtc} /></p>
          {source && <SourcePreview source={source} displayed={sourceDisplayed} onDisplayed={onSourceDisplayed} />}
          {context.canSubmit && source && context.consentText ? (
            <>
              {busy === "viewed" && <p role="status" className="text-sm text-neutral-600">Confirming PDF preview…</p>}
              {sourceDisplayed && context.progress === "Sent" && !busy && (
                <button className="admin-secondary" type="button" onClick={() => advance("viewed")}>Confirm PDF review</button>
              )}
              {sourceDisplayed && context.progress === "Viewed" && (
                <button className="admin-primary" type="button" disabled={!!busy} onClick={() => advance("started")}>
                  {busy === "started" ? "Starting…" : "Begin signing"}
                </button>
              )}
              {sourceDisplayed && context.progress === "Started" && (
                <form method="post" onSubmit={sign} className="border-t border-stone-200 pt-5">
                  <fieldset disabled={!!busy || checkStatus} className="min-w-0 space-y-4">
                    <Field id="typed-name" label="Full name" error={fieldErrors.typedname?.[0]}>
                      <input id="typed-name" name="typedName" className="admin-input" autoComplete="name" required maxLength={200}
                        {...fieldDescription("typed-name", fieldErrors.typedname?.[0])} />
                    </Field>
                    <label className="flex items-start gap-3 text-sm leading-6">
                      <input id="signing-consent" name="consentAccepted" type="checkbox" required
                        className="mt-1 size-4 shrink-0 accent-red-700" {...fieldDescription("signing-consent", consentError)} />
                      <span className="whitespace-pre-wrap wrap-anywhere">{context.consentText}</span>
                    </label>
                    {consentError && <p id="signing-consent-error" role="alert" className="text-sm text-red-800">{consentError}</p>}
                    <button className="admin-primary" type="submit">{busy === "sign" ? "Signing…" : "Sign PDF"}</button>
                  </fieldset>
                </form>
              )}
            </>
          ) : <Feedback>This request is currently closed to signing.</Feedback>}
        </>
      )}
    </section>
  );
}

function SourcePreview({ source, displayed, onDisplayed }: { source: Blob; displayed: boolean; onDisplayed: () => void }) {
  const [url, setUrl] = useState("");
  const [inlineUnavailable, setInlineUnavailable] = useState(() => !navigator.pdfViewerEnabled);
  const [downloadSelected, setDownloadSelected] = useState(false);

  useEffect(() => {
    const objectUrl = URL.createObjectURL(source);
    setUrl(objectUrl);
    return () => URL.revokeObjectURL(objectUrl);
  }, [source]);

  if (!url) return <p role="status" className="text-sm text-neutral-600">Loading PDF preview…</p>;
  return (
    <div className="space-y-3">
      <h2 className="font-semibold">Review the PDF</h2>
      {!inlineUnavailable && <iframe title="PDF to sign" src={url}
        className="block h-[60vh] min-h-72 w-full rounded-lg border border-stone-200 bg-white"
        onLoad={onDisplayed} onError={() => setInlineUnavailable(true)} />}
      <a className="admin-secondary" href={url} download="document-to-sign.pdf" onClick={() => setDownloadSelected(true)}>
        Download PDF to review
      </a>
      {!displayed && (inlineUnavailable || downloadSelected) && (
        <div className="space-y-3">
          <p className="text-sm text-neutral-700">Open the downloaded PDF before continuing.</p>
          <button className="admin-secondary" type="button" disabled={!downloadSelected} onClick={onDisplayed}>I’ve opened the PDF</button>
        </div>
      )}
    </div>
  );
}
