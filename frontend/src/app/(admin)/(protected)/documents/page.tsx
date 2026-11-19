import { notFound } from "next/navigation";
import { DocumentLibrary } from "@/features/documents/document-library";
import { parseRouteId } from "@/features/documents/document-types";

export default async function DocumentsPage({ searchParams }: PageProps<"/documents">) {
  const { matterId } = await searchParams;
  const scope = parseRouteId(matterId);
  if (matterId !== undefined && scope === null) notFound();

  return <DocumentLibrary key={scope ?? "firm"} matterId={scope} />;
}
