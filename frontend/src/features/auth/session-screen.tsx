"use client";

import { Brand } from "@/components/brand";
import type { ReactNode } from "react";
import { useAdminSession } from "./admin-session";

export function SessionScreen({ children }: { children?: ReactNode }) {
  return (
    <main className="session-canvas">
      <div className="session-layout">
        <div className="session-intro">
          <Brand />
          <div className="session-title"><h2>Firm workspace</h2><p>Manage client matters, documents, and signatures.</p></div>
        </div>
        <div className="session-form">{children}</div>
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
