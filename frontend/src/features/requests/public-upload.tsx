"use client";

import { useEffect, useState, type FormEvent } from "react";
import { Feedback, Timestamp } from "@/components/workspace-ui";
import { PdfFileField, invalidPdfError, pdfSizeError, validatePdfFile } from "@/features/documents/pdf-upload";
import { ApiError } from "@/lib/api";
import type { PublicUploadContext } from "./request-types";
import { requestWithLink, useLinkToken } from "./use-link-token";

const rateLimitMessage = "Too many requests. Wait a minute and try again.";

function UnavailableUpload() {
  return (
    <section className="admin-panel space-y-3 p-5 sm:p-7" role="alert">
      <h1 className="text-2xl font-semibold tracking-tight">Upload link unavailable</h1>
      <p className="text-sm leading-6 text-neutral-700">Open the most recent link you received or contact the sender for help.</p>
    </section>
  );
}

export function PublicUploadPage({ requestId }: { requestId: string | null }) {
  const link = useLinkToken();
  if (!requestId || (link && !link.token)) return <UnavailableUpload />;
  if (!link?.token) return <p role="status">Loading upload request…</p>;
  return <PublicUpload key={link.revision} requestId={requestId} token={link.token} />;
}

function PublicUpload({ requestId, token }: { requestId: string; token: string }) {
  const path = `/external/upload/${requestId}` as const;
  const [context, setContext] = useState<PublicUploadContext | null>(null);
  const [pending, setPending] = useState(true);
  const [attempt, setAttempt] = useState(0);
  const [unavailable, setUnavailable] = useState(false);
  const [loadError, setLoadError] = useState("");
  const [file, setFile] = useState<File | null>(null);
  const [fileError, setFileError] = useState("");
  const [feedback, setFeedback] = useState("");
  const [submitting, setSubmitting] = useState(false);

  useEffect(() => {
    const controller = new AbortController();
    const { signal } = controller;
    queueMicrotask(() => {
      if (signal.aborted) return;
      void requestWithLink<PublicUploadContext>(path, token, { signal }).then((data) => {
        if (!signal.aborted) setContext(data);
      }).catch((error: unknown) => {
        if (signal.aborted) return;
        if (error instanceof ApiError && error.status === 404) setUnavailable(true);
        else setLoadError(error instanceof ApiError && error.status === 429
          ? rateLimitMessage : "Couldn’t load the upload request. Try again.");
      }).finally(() => {
        if (!signal.aborted) setPending(false);
      });
    });
    return () => controller.abort();
  }, [path, token, attempt]);

  function refreshContext() {
    setPending(true);
    setLoadError("");
    setAttempt((current) => current + 1);
  }

  async function upload(event: FormEvent<HTMLFormElement>) {
    event.preventDefault();
    if (!context?.canSubmit || pending || submitting || loadError) return;
    setFeedback("");
    const invalid = validatePdfFile(file);
    setFileError(invalid ?? "");
    if (invalid || !file) return;

    setSubmitting(true);
    const body = new FormData();
    body.set("file", file);
    try {
      setContext(await requestWithLink<PublicUploadContext>(path, token, { method: "POST", body }));
      setFile(null);
    } catch (error) {
      if (error instanceof ApiError && error.status === 404) setUnavailable(true);
      else if (error instanceof ApiError && error.status === 413) setFileError(pdfSizeError);
      else if (error instanceof ApiError && error.status === 400) setFileError(error.fieldErrors.file?.[0] ?? invalidPdfError);
      else if (error instanceof ApiError && error.status === 409) {
        setFeedback("The request changed while you were uploading. Check the current instructions and try again.");
        refreshContext();
      } else {
        setFeedback(error instanceof ApiError && error.status === 429 ? rateLimitMessage : "Couldn’t confirm the upload. Check the request status before trying again.");
      }
    } finally {
      setSubmitting(false);
    }
  }

  if (unavailable) return <UnavailableUpload />;
  const contextFeedback = (
    <>
      {pending && <p role="status" className="text-sm text-neutral-600">{context ? "Checking request status…" : "Loading upload request…"}</p>}
      {loadError && <div className="space-y-3"><Feedback kind="error">{loadError}</Feedback>
        <button className="admin-secondary" type="button" onClick={refreshContext}>Try again</button></div>}
    </>
  );
  if (!context) return <section className="admin-panel p-5 sm:p-7">{contextFeedback}</section>;

  return (
    <section className="admin-panel space-y-5 p-5 sm:p-7">
      <div className="space-y-2">
        <p className="text-sm font-medium text-neutral-600">Document request</p>
        <h1 className="text-3xl font-semibold tracking-tight wrap-anywhere">{context.title}</h1>
        <p className="text-sm text-neutral-700 wrap-anywhere">Requested from {context.recipientName}</p>
      </div>
      {contextFeedback}
      {context.canSubmit ? (
        <>
          {context.instructions && <p className="whitespace-pre-wrap text-sm leading-6 text-neutral-800 wrap-anywhere">{context.instructions}</p>}
          {context.correctionExplanation && (
            <div className="space-y-1 rounded-lg border border-stone-200 bg-stone-50 p-4">
              <h2 className="font-semibold">Changes requested</h2>
              <p className="whitespace-pre-wrap text-sm leading-6 text-neutral-800 wrap-anywhere">{context.correctionExplanation}</p>
            </div>
          )}
          <p className="text-sm text-neutral-600">Link expires <Timestamp value={context.expiresAtUtc} /></p>
          {feedback && <div className="space-y-3"><Feedback kind="error">{feedback}</Feedback>
            {!pending && !loadError && <button className="admin-secondary" type="button" onClick={refreshContext}>Check request status</button>}</div>}
          <form method="post" onSubmit={upload} className="border-t border-stone-200 pt-5">
            <fieldset disabled={pending || submitting || !!loadError} className="min-w-0 space-y-4">
              <PdfFileField error={fileError || undefined} onChange={(selected) => { setFile(selected); setFileError(""); }} />
              <button className="admin-primary" type="submit">{submitting ? "Uploading…" : "Submit PDF"}</button>
            </fieldset>
          </form>
        </>
      ) : (
        <Feedback>{context.uploadStatus === "Accepted" ? "Your PDF was accepted."
          : context.uploadStatus === "Submitted" ? "Your PDF was received and is awaiting review."
          : "This request is currently closed to uploads."}</Feedback>
      )}
    </section>
  );
}
