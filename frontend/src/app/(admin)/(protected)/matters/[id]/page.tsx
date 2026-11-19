import { notificationReturnHref } from "@/features/notifications/notification-location";
import { notFound } from "next/navigation";
import { MatterDetails } from "@/features/matters/matter-page";
import { parseRouteId } from "@/lib/route-params";

export default async function MatterPage({ params, searchParams }: PageProps<"/matters/[id]">) {
  const { id } = await params;
  const matterId = parseRouteId(id);
  if (matterId === null) notFound();

  const { created, fromNotifications, notificationPage, taskId, deadlineId } = await searchParams;
  const task = parseRouteId(taskId);
  const deadline = parseRouteId(deadlineId);
  const target = task !== null ? { collection: "tasks" as const, id: task }
    : deadline !== null ? { collection: "deadlines" as const, id: deadline } : undefined;
  return <MatterDetails key={id} id={matterId} created={created === "1"}
    notificationsHref={notificationReturnHref(fromNotifications, notificationPage)} target={target} />;
}
