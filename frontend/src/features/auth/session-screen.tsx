"use client";

import type { ReactNode } from "react";
import { useAdminSession } from "./admin-session";

export function SessionScreen({ children }: { children?: ReactNode }) {
  return (
    <main className="relative isolate grid min-h-dvh place-items-center bg-stone-50 px-5 py-12 sm:px-8">
      <div className="w-full max-w-sm">
        <div className="mb-6 flex items-baseline justify-between gap-x-4 gap-y-2">
          <p className="text-xl font-bold tracking-tight">Amanet</p>
          <p className="text-sm text-neutral-600">Admin workspace</p>
        </div>
        <div className="rounded-2xl border border-stone-200 bg-white p-6 shadow-sm sm:p-8">
          {children}
        </div>
      </div>
    </main>
  );
}

export function SessionStatus({ pending }: { pending: string }) {
  const { session, retry, signOut } = useAdminSession();

  if (session.status === "unavailable") {
    return (
      <SessionScreen>
        <h1 className="text-xl font-semibold tracking-tight">Couldn’t check your session</h1>
        <p role="alert" className="mt-2 text-sm leading-6 text-neutral-600">
          The server is unavailable. Try again or sign out of this tab.
        </p>
        <div className="mt-5 flex flex-wrap items-center gap-4">
          <button type="button" onClick={retry} className="admin-primary">
            Try again
          </button>
          <button type="button" onClick={signOut} className="admin-secondary">
            Sign out
          </button>
        </div>
      </SessionScreen>
    );
  }

  return (
    <SessionScreen>
      <p role="status" className="text-sm text-neutral-600">
        {pending}
      </p>
    </SessionScreen>
  );
}
