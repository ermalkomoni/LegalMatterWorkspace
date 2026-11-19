const tones: Record<string, string> = {
  Open: "green", Active: "green", Accepted: "green", Signed: "green", Completed: "green",
  "In progress": "blue", Started: "blue", Viewed: "violet", Sent: "blue",
  "On hold": "amber", Pending: "amber", "Awaiting upload": "amber", "Awaiting review": "amber",
  "Corrections requested": "rose", Overdue: "rose", Revoked: "rose", Failed: "rose",
  High: "amber", Urgent: "rose",
};

export function StatusPill({ value }: { value: string }) {
  return <span className="status-pill" data-tone={tones[value] ?? "neutral"}>{value}</span>;
}
