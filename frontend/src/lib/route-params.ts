export function parseRouteId(value: string | string[] | undefined): number | null {
  if (typeof value !== "string" || !/^\d+$/.test(value)) return null;
  const id = Number(value);
  return id >= 1 && id <= 2147483647 ? id : null;
}
