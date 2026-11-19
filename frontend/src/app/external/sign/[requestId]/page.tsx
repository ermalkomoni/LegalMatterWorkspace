import { PublicSignPage } from "@/features/requests/public-sign";
import { isRequestId } from "@/features/requests/request-types";

export default async function SignPage({ params }: PageProps<"/external/sign/[requestId]">) {
  const { requestId } = await params;
  return <PublicSignPage key={requestId} requestId={isRequestId(requestId) ? requestId : null} />;
}
