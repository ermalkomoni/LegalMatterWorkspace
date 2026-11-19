import { LoadError, Pagination, Timestamp } from "@/components/workspace-ui";
import type { PagedResult } from "@/lib/pagination";

export type MatterActivity = { id: number; description: string; occurredAtUtc: string };

export function MatterHistory({ data, pending, error, onRetry, onPage }: {
  data?: PagedResult<MatterActivity>;
  pending: boolean;
  error?: Error;
  onRetry: () => void;
  onPage: (page: number) => void;
}) {
  return (
    <section id="history" aria-labelledby="history-heading" tabIndex={-1} className="admin-panel scroll-mt-4">
      <div className="p-4 sm:px-6 sm:py-5">
        <h2 id="history-heading" className="text-xl font-semibold tracking-tight">Matter history</h2>
        {pending && <p role="status" className="mt-2 text-sm text-neutral-600">{data ? "Refreshing history…" : "Loading history…"}</p>}
        {error && <div className="mt-2"><LoadError error={error} resource="matter history" onRetry={onRetry} compact /></div>}
        {data?.items.length === 0 && <p className="mt-2 text-sm text-neutral-600">No activity recorded.</p>}
      </div>
      {!!data?.items.length && (
        <ol className="divide-y divide-stone-200 border-t border-stone-200">
          {data.items.map((activity) => (
            <li key={activity.id} className="grid gap-1 px-4 py-3 sm:grid-cols-[1fr_auto] sm:gap-5 sm:px-6">
              <p className="text-sm font-medium wrap-anywhere">{activity.description}</p>
              <p className="text-sm text-neutral-600"><Timestamp value={activity.occurredAtUtc} /></p>
            </li>
          ))}
        </ol>
      )}
      {data && data.totalCount > data.pageSize && (
        <div className="border-t border-stone-200 px-4 pb-4 sm:px-6">
          <Pagination result={data} onPage={onPage} />
        </div>
      )}
    </section>
  );
}
