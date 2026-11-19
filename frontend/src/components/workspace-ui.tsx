import Link from "next/link";
import type { ReactNode } from "react";
import { ApiError } from "@/lib/api";
import type { PagedResult } from "@/lib/pagination";

export function PageHeading({ title, action }: { title: string; action?: ReactNode }) {
  return (
    <div className="mb-6 flex flex-wrap items-center justify-between gap-4 sm:mb-8">
      <h1 className="min-w-0 text-3xl font-semibold tracking-tight wrap-anywhere sm:text-4xl">{title}</h1>
      {action}
    </div>
  );
}

export function BackLink({ href, children }: { href: string; children: ReactNode }) {
  return (
    <Link href={href} className="admin-link mb-4 inline-flex items-center gap-2 text-sm">
      <span aria-hidden="true">←</span>{children}
    </Link>
  );
}

export function Field({
  id, label, error, children,
}: {
  id: string;
  label: string;
  error?: string;
  children: ReactNode;
}) {
  return (
    <div className="min-w-0">
      <label htmlFor={id} className="block text-sm font-medium">{label}</label>
      {children}
      {error && <p role="alert" id={`${id}-error`} className="mt-1.5 text-sm text-red-800">{error}</p>}
    </div>
  );
}

export function fieldDescription(id: string, error?: string) {
  return {
    "aria-invalid": error ? true as const : undefined,
    "aria-describedby": error ? `${id}-error` : undefined,
  };
}

export function Feedback({ children, kind = "status" }: {
  children: ReactNode;
  kind?: "status" | "error";
}) {
  return (
    <p
      role={kind === "error" ? "alert" : "status"}
      className={`rounded-lg px-3 py-2.5 text-sm leading-5 ${kind === "error"
        ? "bg-red-50 text-red-800"
        : "bg-stone-100 text-neutral-800"}`}
    >
      {children}
    </p>
  );
}

export function DetailValue({ label, children, className = "" }: {
  label: string;
  children: ReactNode;
  className?: string;
}) {
  return (
    <div className={`min-w-0 ${className}`}>
      <dt className="text-sm font-medium text-neutral-600">{label}</dt>
      <dd className="mt-1 whitespace-pre-wrap text-neutral-950 wrap-anywhere">{children ?? "—"}</dd>
    </div>
  );
}

export function LoadError({ error, onRetry, resource, compact = false }: {
  error: unknown;
  onRetry: () => void;
  resource: string;
  compact?: boolean;
}) {
  if (error instanceof ApiError && error.status === 404) {
    return <Feedback kind="error">{resource} could not be found.</Feedback>;
  }
  return (
    <div className={`flex flex-wrap items-center gap-3 ${compact ? "" : "admin-panel p-4"}`}>
      <p role="alert" className="text-sm text-red-800">Couldn’t load {resource}. Try again.</p>
      <button className="admin-secondary" type="button" onClick={() => onRetry()}>Try again</button>
    </div>
  );
}

export function Pagination({ result, onPage }: {
  result: PagedResult<unknown>;
  onPage: (page: number) => void;
}) {
  const lastPage = Math.max(1, Math.ceil(result.totalCount / result.pageSize));
  if (lastPage === 1 && result.page === 1) return null;
  return (
    <nav aria-label="Pagination" className="mt-4 flex flex-wrap items-center justify-between gap-3">
      <p className="text-sm text-neutral-700">Page {result.page} of {lastPage}</p>
      <div className="flex gap-2">
        <button type="button" className="admin-secondary" disabled={result.page <= 1}
          onClick={() => onPage(result.page - 1)}>Previous</button>
        <button type="button" className="admin-secondary" disabled={result.page >= lastPage}
          onClick={() => onPage(result.page + 1)}>Next</button>
      </div>
    </nav>
  );
}
