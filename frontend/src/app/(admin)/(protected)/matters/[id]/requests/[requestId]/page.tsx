import { notificationReturnHref } from "@/features/notifications/notification-location";
import { notFound } from "next/navigation";
import { RequestDetails } from "@/features/requests/request-details";
import { isRequestId } from "@/features/requests/request-types";
import { parseRouteId } from "@/lib/route-params";

export default async function RequestPage({ params, searchParams }: PageProps<"/matters/[id]/requests/[requestId]">) {
  const { id, requestId } = await params;
  const matterId = parseRouteId(id);
  if (matterId === null || !isRequestId(requestId)) notFound();
  const { created, fromNotifications, notificationPage } = await searchParams;
  return <RequestDetails key={`${matterId}-${requestId}`} matterId={matterId} requestId={requestId}
    created={created === "1"} notificationsHref={notificationReturnHref(fromNotifications, notificationPage)} />;
}
