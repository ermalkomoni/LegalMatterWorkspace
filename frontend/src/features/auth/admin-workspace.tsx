"use client";

import Link from "next/link";
import { usePathname, useRouter } from "next/navigation";
import { useEffect, type ReactNode } from "react";
import { useAdminSession } from "./admin-session";
import { SessionStatus } from "./session-screen";

export function AdminWorkspace({ children }: { children: ReactNode }) {
  const { session, signOut } = useAdminSession();
  const router = useRouter();
  const pathname = usePathname();

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
        <nav aria-label="Workspace" className="mx-auto flex max-w-6xl gap-6 px-5 sm:px-8">
          {[
            { label: "Matters", href: "/", active: pathname === "/" || pathname.startsWith("/matters") },
            { label: "Clients", href: "/clients", active: pathname.startsWith("/clients") },
            { label: "Documents", href: "/documents", active: pathname.startsWith("/documents") },
          ].map(({ label, href, active }) => (
            <Link key={href} href={href} aria-current={pathname === href ? "page" : active ? "location" : undefined}
              className={`border-b-2 px-1 py-3 text-sm font-semibold focus-visible:outline-2 focus-visible:outline-offset-2 focus-visible:outline-red-700 ${active
                ? "border-red-700 text-neutral-950"
                : "border-transparent text-neutral-600 hover:text-neutral-950"}`}>
              {label}
            </Link>
          ))}
        </nav>
      </header>
      <main className="mx-auto max-w-6xl px-5 py-10 sm:px-8 sm:py-14">
        {children}
      </main>
    </div>
  );
}
