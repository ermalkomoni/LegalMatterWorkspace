"use client";

import { useEffect } from "react";
import { useRouter } from "next/navigation";
import { PageHeading, Pagination } from "@/components/workspace-ui";
import { Icon } from "@/components/icon";
import { notificationsHref, type FeedKind } from "./notification-location";
import { FeedSwitch, NotificationFeed, useNotifications } from "./notification-feed";

export function NotificationList({ page, kind }: { page: number; kind: FeedKind }) {
  const router = useRouter();
  const result = useNotifications(kind, page, 20);
  const { data, refresh } = result;
  const lastPage = data && Math.max(1, Math.ceil(data.totalCount / data.pageSize));
  useEffect(() => {
    if (lastPage && page > lastPage) router.replace(notificationsHref(kind, lastPage));
  }, [kind, lastPage, page, router]);
  useEffect(() => {
    const revalidate = (event: PageTransitionEvent) => { if (event.persisted) refresh(); };
    window.addEventListener("pageshow", revalidate);
    return () => window.removeEventListener("pageshow", revalidate);
  }, [refresh]);
  return <>
    <PageHeading title="Notifications" description="Review outstanding work and recent document activity." />
    <div className="feed-toolbar">
      <FeedSwitch kind={kind} onChange={(next) => router.push(notificationsHref(next, 1))} />
      <button className="icon-button" aria-label="Refresh notifications" title="Refresh notifications"
        disabled={result.pending} onClick={() => refresh()}><Icon name="refresh" /></button>
    </div>
    <div className="admin-panel"><NotificationFeed kind={kind} result={result} /></div>
    {data && <Pagination result={data} onPage={(next) => router.push(notificationsHref(kind, next))} />}
  </>;
}
