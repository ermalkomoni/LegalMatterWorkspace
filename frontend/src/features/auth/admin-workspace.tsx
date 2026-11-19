"use client";

import Link from "next/link";
import { usePathname, useRouter } from "next/navigation";
import { useEffect, type ReactNode } from "react";
import { Brand } from "@/components/brand";
import { Icon } from "@/components/icon";
import { useAdminSession } from "./admin-session";
import { HeaderTools } from "./header-tools";
import { SessionStatus } from "./session-screen";

const destinations = [
  { label: "Matters", href: "/", prefix: "/matters", icon: "folder" },
  { label: "Clients", href: "/clients", prefix: "/clients", icon: "users" },
  { label: "Documents", href: "/documents", prefix: "/documents", icon: "file" },
] as const;

export function AdminWorkspace({ children }: { children: ReactNode }) {
  const { session, signOut } = useAdminSession();
  const router = useRouter();
  const pathname = usePathname();
  useEffect(() => {
    if (session.status === "signed-out") router.replace("/login");
  }, [router, session.status]);
  if (session.status !== "signed-in") return <SessionStatus
    pending={session.status === "signed-out" ? "Opening sign-in…" : "Checking your session…"} />;
  return <div className="workspace">
    <a className="skip-link" href="#content">Skip to content</a>
    <header className="workspace-header"><div className="header-inner">
      <Link className="brand-link" href="/" aria-label="Amanet home"><Brand /></Link>
      <nav className="main-nav" aria-label="Workspace">{destinations.map(({ href, label, prefix, icon }) =>
        <Link key={href} href={href} aria-current={pathname === href ? "page" : pathname.startsWith(prefix) ? "location" : undefined}>
          <Icon name={icon} size={17} />{label}
        </Link>)}</nav>
      <HeaderTools key={pathname} email={session.admin.email} signOut={signOut} />
    </div></header>
    <main className="workspace-main" id="content" tabIndex={-1}>{children}</main>
  </div>;
}
