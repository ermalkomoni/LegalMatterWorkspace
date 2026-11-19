import { NotificationList } from "@/features/notifications/notification-list";
import { parseRouteId } from "@/lib/route-params";

export default async function NotificationsPage({ searchParams }: PageProps<"/notifications">) {
  const { page } = await searchParams;
  return <NotificationList page={parseRouteId(page) ?? 1} />;
}
