import { notFound } from "next/navigation";
import { DocumentDetails } from "@/features/documents/document-details";
import { parseRouteId } from "@/lib/route-params";

export default async function DocumentPage({ params, searchParams }: PageProps<"/documents/[matterId]/[documentId]">) {
  const { matterId, documentId } = await params;
  const matter = parseRouteId(matterId);
  const document = parseRouteId(documentId);
  if (matter === null || document === null) notFound();

  const { fromMatter, uploaded } = await searchParams;
  return <DocumentDetails key={`${matter}-${document}`} matterId={matter} documentId={document}
    fromMatter={fromMatter === "1"} uploaded={uploaded === "1"} />;
}
