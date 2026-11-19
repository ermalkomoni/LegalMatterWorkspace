import { NotificationList } from "@/features/notifications/notification-list";
import { parseRouteId } from "@/lib/route-params";

export default async function NotificationsPage({ searchParams }: PageProps<"/notifications">) {
  const { page, view } = await searchParams;
  return <NotificationList kind={view === "activity" ? "activity" : "pending"} page={parseRouteId(page) ?? 1} />;
}
