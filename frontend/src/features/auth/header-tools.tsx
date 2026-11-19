"use client";

import Link from "next/link";
import { useEffect, useRef, useState } from "react";
import { Icon } from "@/components/icon";
import type { FeedKind } from "@/features/notifications/notification-location";
import { FeedSwitch, NotificationFeed, useNotifications } from "@/features/notifications/notification-feed";

export function HeaderTools({ email, signOut }: { email: string; signOut: () => void }) {
  const [panel, setPanel] = useState<"bell" | "account" | null>(null);
  const [kind, setKind] = useState<FeedKind>("pending");
  const root = useRef<HTMLDivElement>(null);
  const bell = useRef<HTMLButtonElement>(null);
  const account = useRef<HTMLButtonElement>(null);
  const actions = useNotifications("pending", 1, 4);
  const activity = useNotifications("activity", 1, 4, panel === "bell" && kind === "activity");
  const refresh = actions.refresh;
  const count = actions.error ? undefined : actions.data?.totalCount;

  useEffect(() => {
    const update = () => { if (!document.hidden) refresh(); };
    const timer = window.setInterval(update, 60000);
    window.addEventListener("focus", update);
    window.addEventListener("amanet:workspace-updated", update);
    return () => {
      window.clearInterval(timer);
      window.removeEventListener("focus", update);
      window.removeEventListener("amanet:workspace-updated", update);
    };
  }, [refresh]);
  useEffect(() => {
    if (!panel) return;
    const dismiss = (event: PointerEvent) => {
      if (!root.current?.contains(event.target as Node)) setPanel(null);
    };
    document.addEventListener("pointerdown", dismiss);
    return () => document.removeEventListener("pointerdown", dismiss);
  }, [panel]);

  return <div className="header-tools" ref={root} onBlur={(event) => {
    if (!event.currentTarget.contains(event.relatedTarget)) setPanel(null);
  }} onKeyDown={(event) => {
    if (event.key === "Escape" && panel) {
      (panel === "bell" ? bell : account).current?.focus();
      setPanel(null);
    }
  }}>
    <div className="tool-anchor">
      <button className="icon-button bell" ref={bell} aria-expanded={panel === "bell"} aria-controls={panel === "bell" ? "bell-panel" : undefined}
        aria-label="Notifications" aria-describedby={count === undefined ? undefined : "pending-count"} title={count === undefined ? "Notifications" : `${count} pending actions`}
        onClick={() => { if (panel !== "bell") { setKind("pending"); refresh(); } setPanel(panel === "bell" ? null : "bell"); }}>
        <Icon name="bell" size={20} />{!!count && <span className="bell-count" aria-hidden="true">{count > 99 ? "99+" : count}</span>}
      </button>
      {count !== undefined && <span id="pending-count" className="sr-only">{count} pending actions</span>}
      {panel === "bell" && <section className="popover bell-panel" id="bell-panel" aria-label="Notifications">
        <h2>Notifications</h2>
        <FeedSwitch kind={kind} onChange={setKind} />
        <NotificationFeed kind={kind} result={kind === "pending" ? actions : activity} onNavigate={() => setPanel(null)} />
        <Link className="popover-footer" href={kind === "activity" ? "/notifications?view=activity" : "/notifications"} onClick={() => setPanel(null)}>
          View all notifications <Icon name="arrow" size={16} />
        </Link>
      </section>}
    </div>
    <div className="tool-anchor">
      <button className="avatar" ref={account} aria-label="Account" title="Account" aria-expanded={panel === "account"}
        aria-controls={panel === "account" ? "account-panel" : undefined} onClick={() => setPanel(panel === "account" ? null : "account")}>
        <Icon name="user" size={19} />
      </button>
      {panel === "account" && <section className="popover account-panel" id="account-panel" aria-label="Account details">
        <small>Administrator</small><strong>{email}</strong>
        <button type="button" className="popover-footer" onClick={signOut}><Icon name="exit" size={16} /> Sign out</button>
      </section>}
    </div>
  </div>;
}
