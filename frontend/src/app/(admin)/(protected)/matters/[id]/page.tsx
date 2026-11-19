import { notFound } from "next/navigation";
import { MatterDetails } from "@/features/matters/matter-page";

export default async function MatterPage({ params, searchParams }: PageProps<"/matters/[id]">) {
  const { id } = await params;
  if (!/^\d+$/.test(id) || Number(id) < 1 || Number(id) > 2147483647) notFound();

  const { created } = await searchParams;
  return <MatterDetails key={id} id={Number(id)} created={created === "1"} />;
}
