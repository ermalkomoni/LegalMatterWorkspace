import { parseRouteId } from "@/lib/route-params";

export type FeedKind = "pending" | "activity";

export function notificationsHref(kind: FeedKind, page: number) {
  const query = new URLSearchParams();
  if (kind === "activity") query.set("view", kind);
  if (page > 1) query.set("page", String(page));
  return query.size ? `/notifications?${query}` : "/notifications";
}

export function notificationReturnHref(source: string | string[] | undefined, page: string | string[] | undefined) {
  if (source !== "1" && source !== "activity") return null;
  return notificationsHref(source === "activity" ? "activity" : "pending", parseRouteId(page) ?? 1);
}
