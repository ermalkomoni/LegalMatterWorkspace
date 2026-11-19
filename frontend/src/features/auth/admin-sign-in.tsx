"use client";

import { useRouter } from "next/navigation";
import { useEffect, useState, type FormEvent } from "react";
import { ApiError } from "@/lib/api";
import { useAdminSession } from "./admin-session";
import { SessionScreen, SessionStatus } from "./session-screen";

export function AdminSignIn() {
  const { session, signIn } = useAdminSession();
  const router = useRouter();
  const [email, setEmail] = useState("");
  const [password, setPassword] = useState("");
  const [submitting, setSubmitting] = useState(false);
  const [feedback, setFeedback] = useState("");
  const [fieldErrors, setFieldErrors] = useState<ApiError["fieldErrors"]>({});

  useEffect(() => {
    if (session.status === "signed-in") router.replace("/");
  }, [router, session.status]);

  async function submit(event: FormEvent<HTMLFormElement>) {
    event.preventDefault();
    if (submitting) return;
    setFeedback("");
    setFieldErrors({});
    setSubmitting(true);

    try {
      await signIn({ email: email.trim(), password });
      setPassword("");
    } catch (error) {
      if (error instanceof ApiError && error.status === 400) {
        setFieldErrors(error.fieldErrors);
        setFeedback("Check the sign-in details and try again.");
      } else if (error instanceof ApiError && error.status === 401) {
        setFeedback("Invalid email or password.");
      } else if (error instanceof ApiError && error.status === 429) {
        setFeedback("Too many sign-in attempts. Wait a minute and try again.");
      } else {
        setFeedback("Couldn’t sign in. Please try again.");
      }
    } finally {
      setSubmitting(false);
    }
  }

  if (session.status !== "signed-out") {
    return (
      <SessionStatus
        pending={session.status === "signed-in" ? "Opening your workspace…" : "Checking your session…"}
      />
    );
  }

  const emailError = fieldErrors.email?.[0];
  const passwordError = fieldErrors.password?.[0];

  return (
    <SessionScreen>
      <h1 className="text-3xl font-semibold tracking-tight">Sign in</h1>
      {session.notice && (
        <p role="status" className="mt-4 text-sm leading-6 text-neutral-700">
          {session.notice}
        </p>
      )}
      <form method="post" onSubmit={submit} className="mt-7 space-y-5">
        <div>
          <label htmlFor="email" className="block text-sm font-medium">Email</label>
          <input
            id="email"
            name="email"
            type="email"
            autoComplete="username"
            required
            maxLength={256}
            value={email}
            onChange={(event) => setEmail(event.target.value)}
            aria-invalid={emailError ? true : undefined}
            aria-describedby={emailError ? "email-error" : undefined}
            className="admin-input"
          />
          {emailError && <p id="email-error" className="mt-1.5 text-sm text-red-800">{emailError}</p>}
        </div>
        <div>
          <label htmlFor="password" className="block text-sm font-medium">Password</label>
          <input
            id="password"
            name="password"
            type="password"
            autoComplete="current-password"
            required
            maxLength={1024}
            value={password}
            onChange={(event) => setPassword(event.target.value)}
            aria-invalid={passwordError ? true : undefined}
            aria-describedby={passwordError ? "password-error" : undefined}
            className="admin-input"
          />
          {passwordError && (
            <p id="password-error" className="mt-1.5 text-sm text-red-800">{passwordError}</p>
          )}
        </div>
        {feedback && (
          <p role="alert" className="rounded-lg bg-red-50 px-3 py-2.5 text-sm leading-5 text-red-800">
            {feedback}
          </p>
        )}
        <button type="submit" disabled={submitting} className="admin-primary w-full">
          {submitting ? "Signing in…" : "Sign in"}
        </button>
      </form>
    </SessionScreen>
  );
}
