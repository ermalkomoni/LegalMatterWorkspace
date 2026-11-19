import { PublicUploadPage } from "@/features/requests/public-upload";
import { isRequestId } from "@/features/requests/request-types";

export default async function UploadPage({ params }: PageProps<"/external/upload/[requestId]">) {
  const { requestId } = await params;
  return <PublicUploadPage key={requestId} requestId={isRequestId(requestId) ? requestId : null} />;
}
