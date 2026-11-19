import type { Metadata } from "next";
import type { ReactNode } from "react";

export const metadata: Metadata = {
  referrer: "no-referrer",
  robots: { index: false, follow: false },
};

export default function ExternalLayout({ children }: { children: ReactNode }) {
  return (
    <div className="min-h-dvh bg-stone-50">
      <header className="border-b border-stone-200 bg-white">
        <div className="mx-auto max-w-2xl px-5 py-4 text-xl font-bold tracking-tight sm:px-8">Amanet</div>
      </header>
      <main className="mx-auto max-w-2xl px-5 py-8 sm:px-8 sm:py-14">{children}</main>
    </div>
  );
}
