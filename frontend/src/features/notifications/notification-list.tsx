"use client";

import Link from "next/link";
import { useRouter } from "next/navigation";
import { useEffect } from "react";
import { LoadError, PageHeading, Pagination, Timestamp } from "@/components/workspace-ui";
import { useAdminQuery } from "@/features/auth/use-admin-query";
import { formatCalendarDate } from "@/features/matters/matter-types";
import { requestListHref } from "@/features/requests/request-types";
import { apiPath } from "@/lib/api";
import type { PagedResult } from "@/lib/pagination";

type AdminNotification = {
  id: number | null;
  matterId: number;
  requestId: string | null;
  taskId: number | null;
  deadlineId: number | null;
  kind: string;
  message: string;
  isActionable: boolean;
  createdAtUtc: string | null;
  dueDate: string | null;
};

function notificationHref({ matterId, requestId, taskId, deadlineId }: AdminNotification) {
  const matter = `/matters/${matterId}`;
  if (requestId) return `${requestListHref(matterId)}/${requestId}?fromNotifications=1`;
  if (taskId !== null) return `${matter}?taskId=${taskId}&fromNotifications=1`;
  if (deadlineId !== null) return `${matter}?deadlineId=${deadlineId}&fromNotifications=1`;
  return `${matter}?fromNotifications=1`;
}

function notificationKey(item: AdminNotification) {
  return item.id === null
    ? `pending-${item.matterId}-${item.kind}-${item.requestId ?? item.taskId ?? item.deadlineId}`
    : `activity-${item.id}`;
}

function notificationsHref(page: number) {
  return page === 1 ? "/notifications" : `/notifications?page=${page}`;
}

export function NotificationList({ page }: { page: number }) {
  const router = useRouter();
  const { data, error, pending, refresh } = useAdminQuery<PagedResult<AdminNotification>>(
    apiPath("/notifications", { page, pageSize: 20 }),
  );
  const lastPage = data && Math.max(1, Math.ceil(data.totalCount / data.pageSize));
  const overflow = lastPage !== undefined && page > lastPage;

  useEffect(() => {
    if (overflow && !pending) router.replace(notificationsHref(lastPage));
  }, [lastPage, overflow, pending, router]);

  useEffect(() => {
    function revalidate(event: PageTransitionEvent) {
      if (event.persisted) refresh();
    }
    window.addEventListener("pageshow", revalidate);
    return () => window.removeEventListener("pageshow", revalidate);
  }, [refresh]);

  const actions = data?.items.filter((item) => item.isActionable) ?? [];
  const activity = data?.items.filter((item) => !item.isActionable) ?? [];

  return (
    <>
      <PageHeading title="Notifications" action={
        <button type="button" className="admin-secondary" disabled={pending} onClick={() => refresh()}>
          Refresh notifications
        </button>
      } />
      <div className="space-y-5">
        {(pending || overflow) && <p role="status" className="text-sm text-neutral-600">
          {data ? "Refreshing notifications…" : "Loading notifications…"}
        </p>}
        {error && <LoadError error={error} resource="notifications" onRetry={refresh} />}
        {data && !overflow && (
          <>
            {(actions.length > 0 || page === 1) && <NotificationGroup id="pending-actions" title="Pending actions"
              items={actions} emptyMessage="No pending actions." />}
            {(activity.length > 0 || data.totalCount === 0) && <NotificationGroup id="notification-activity" title="Activity"
              items={activity} emptyMessage="No activity yet." />}
            <Pagination result={data} onPage={(next) => router.push(notificationsHref(next))} />
          </>
        )}
      </div>
    </>
  );
}

function NotificationGroup({ id, title, items, emptyMessage }: {
  id: string;
  title: string;
  items: AdminNotification[];
  emptyMessage: string;
}) {
  return (
    <section aria-labelledby={id} className="admin-panel">
      <h2 id={id} className="p-4 text-xl font-semibold tracking-tight sm:px-6">{title}</h2>
      {items.length ? (
        <ul className="divide-y divide-stone-200 border-t border-stone-200">
          {items.map((item) => (
            <li key={notificationKey(item)} className="grid gap-1 px-4 py-3 text-sm sm:grid-cols-[minmax(0,1fr)_auto] sm:gap-5 sm:px-6">
              <div className="min-w-0 wrap-anywhere">
                <Link href={notificationHref(item)} className="admin-link">{item.message}</Link>
              </div>
              {item.dueDate ? (
                <p className="text-neutral-600">Due <time dateTime={item.dueDate}>{formatCalendarDate(item.dueDate)}</time></p>
              ) : item.createdAtUtc && <p className="text-neutral-600"><Timestamp value={item.createdAtUtc} /></p>}
            </li>
          ))}
        </ul>
      ) : <p role="status" className="px-4 pb-4 text-sm text-neutral-600 sm:px-6">{emptyMessage}</p>}
    </section>
  );
}
