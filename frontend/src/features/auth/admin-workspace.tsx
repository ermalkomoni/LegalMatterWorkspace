"use client";

import { useRouter } from "next/navigation";
import { useEffect, type ReactNode } from "react";
import { useAdminSession } from "./admin-session";
import { SessionStatus } from "./session-screen";

export function AdminWorkspace({ children }: { children: ReactNode }) {
  const { session, signOut } = useAdminSession();
  const router = useRouter();

  useEffect(() => {
    if (session.status === "signed-out") router.replace("/login");
  }, [router, session.status]);

  if (session.status !== "signed-in") {
    return (
      <SessionStatus
        pending={session.status === "signed-out" ? "Opening sign-in…" : "Checking your session…"}
      />
    );
  }

  return (
    <div className="min-h-dvh bg-stone-50">
      <header className="border-b border-stone-200 bg-white">
        <div className="mx-auto flex max-w-6xl flex-wrap items-center justify-between gap-x-8 gap-y-4 px-5 py-4 sm:px-8">
          <p className="text-xl font-bold tracking-tight">Amanet</p>
          <div className="flex min-w-0 grow flex-wrap items-center justify-between gap-x-6 gap-y-3 sm:grow-0 sm:justify-end">
            <div className="min-w-0">
              <p className="text-xs text-neutral-600">Signed in as</p>
              <p className="break-all text-sm font-medium">{session.admin.email}</p>
            </div>
            <button type="button" onClick={signOut} className="admin-secondary">
              Sign out
            </button>
          </div>
        </div>
      </header>
      <main className="mx-auto max-w-6xl px-5 py-10 sm:px-8 sm:py-14">
        {children}
      </main>
    </div>
  );
}
