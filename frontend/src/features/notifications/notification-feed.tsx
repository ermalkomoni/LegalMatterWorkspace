"use client";

import type { FeedKind } from "./notification-location";
import Link from "next/link";
import { Icon } from "@/components/icon";
import { LoadError, Timestamp } from "@/components/workspace-ui";
import { useAdminQuery } from "@/features/auth/use-admin-query";
import { formatCalendarDate } from "@/features/matters/matter-types";
import { requestListHref } from "@/features/requests/request-types";
import { apiPath } from "@/lib/api";
import type { PagedResult } from "@/lib/pagination";

export type AdminNotification = {
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

function notificationHref({ matterId, requestId, taskId, deadlineId, isActionable }: AdminNotification, page: number) {
  const origin = new URLSearchParams({ fromNotifications: isActionable ? "1" : "activity" });
  if (page > 1) origin.set("notificationPage", String(page));
  const matter = `/matters/${matterId}`;
  if (requestId) return `${requestListHref(matterId)}/${requestId}?${origin}`;
  if (taskId !== null) return `${matter}?taskId=${taskId}&${origin}`;
  if (deadlineId !== null) return `${matter}?deadlineId=${deadlineId}&${origin}`;
  return `${matter}?${origin}`;
}

function notificationKey(item: AdminNotification) {
  return item.id === null
    ? `pending-${item.matterId}-${item.kind}-${item.requestId ?? item.taskId ?? item.deadlineId}`
    : `activity-${item.id}`;
}



export const feedLabels = { pending: "Pending actions", activity: "Activity" };

export function useNotifications(kind: FeedKind, page: number, pageSize: number, enabled = true) {
  return useAdminQuery<PagedResult<AdminNotification>>(enabled
    ? apiPath("/notifications", { isActionable: String(kind === "pending"), page, pageSize }) : null);
}

export function FeedSwitch({ kind, onChange }: { kind: FeedKind; onChange: (kind: FeedKind) => void }) {
  return <div className="segmented" role="group" aria-label="Notification type">
    {(["pending", "activity"] as const).map((value) =>
      <button key={value} type="button" aria-pressed={kind === value} onClick={() => onChange(value)}>
        <Icon name={value === "pending" ? "inbox" : "clock"} size={16} />{feedLabels[value]}
      </button>)}
  </div>;
}

export function NotificationFeed({ kind, result, onNavigate }: {
  kind: FeedKind; result: ReturnType<typeof useNotifications>; onNavigate?: () => void;
}) {
  const { pending, data, error, refresh } = result;
  return <section aria-label={feedLabels[kind]} className="feed">
    {pending && <p role="status" className="feed-message">Updating notifications…</p>}
    {error && <LoadError resource="notifications" error={error} onRetry={refresh} compact />}
    {data && (data.items.length === 0
      ? <p className="feed-message">{kind === "pending" ? "No pending actions." : "No activity yet."}</p>
      : <ul>{data.items.map((item) => <li key={notificationKey(item)}>
        <Link href={notificationHref(item, data.page)} onClick={onNavigate} className="feed-row" aria-label={item.message}>
          <span className="item-icon"><Icon name={kind === "activity" ? "clock" : item.dueDate ? "clock" : "file"} /></span>
          <span className="min-w-0"><strong>{item.message}</strong><small>{item.dueDate
            ? <>Due {formatCalendarDate(item.dueDate)}</> : item.createdAtUtc && <Timestamp value={item.createdAtUtc} />}</small></span>
          <Icon name="next" size={14} />
        </Link>
      </li>)}</ul>)}
  </section>;
}
