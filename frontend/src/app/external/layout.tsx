import { Brand } from "@/components/brand";
import { Icon } from "@/components/icon";
import type { Metadata } from "next";
import type { ReactNode } from "react";

export const metadata: Metadata = {
  referrer: "no-referrer",
  robots: { index: false, follow: false },
};

export default function ExternalLayout({ children }: { children: ReactNode }) {
  return (
    <div className="external-canvas">
      <header className="workspace-header"><div className="external-header"><Brand />
        <span><Icon name="lock" size={15} /> Client portal</span>
      </div></header>
      <main className="external-main">{children}</main>
    </div>
  );
}
