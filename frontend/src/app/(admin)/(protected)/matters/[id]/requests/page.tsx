import { notFound } from "next/navigation";
import { RequestList } from "@/features/requests/request-list";
import { parseRouteId } from "@/lib/route-params";

export default async function RequestsPage({ params }: PageProps<"/matters/[id]/requests">) {
  const { id } = await params;
  const matterId = parseRouteId(id);
  if (matterId === null) notFound();
  return <RequestList key={matterId} matterId={matterId} />;
}
