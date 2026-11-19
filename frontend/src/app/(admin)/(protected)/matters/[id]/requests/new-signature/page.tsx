import { notFound } from "next/navigation";
import { SigningInvitation } from "@/features/requests/signing-invitation";
import { parseRouteId } from "@/lib/route-params";

export default async function SigningInvitationPage({ params, searchParams }: PageProps<"/matters/[id]/requests/new-signature">) {
  const [{ id }, { documentId, versionId }] = await Promise.all([params, searchParams]);
  const matter = parseRouteId(id);
  const document = parseRouteId(documentId);
  const version = parseRouteId(versionId);
  if (matter === null || document === null || version === null) notFound();

  return <SigningInvitation key={`${matter}-${document}-${version}`} matterId={matter} documentId={document} versionId={version} />;
}
