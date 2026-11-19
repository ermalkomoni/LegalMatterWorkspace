import { notFound } from "next/navigation";
import { ClientDetails } from "@/features/clients/client-page";

export default async function ClientPage({ params, searchParams }: PageProps<"/clients/[id]">) {
  const { id } = await params;
  if (!/^\d+$/.test(id) || Number(id) < 1 || Number(id) > 2147483647) notFound();

  const { created } = await searchParams;
  return <ClientDetails key={id} id={Number(id)} created={created === "1"} />;
}
