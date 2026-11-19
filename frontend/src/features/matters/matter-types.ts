export const matterStatuses = { Open: "Open", InProgress: "In progress", OnHold: "On hold", Closed: "Closed" } as const;
export const matterPriorities = { Low: "Low", Normal: "Normal", High: "High", Urgent: "Urgent" } as const;
export const archiveStates = { Active: "Active", Archived: "Archived", All: "All matters" } as const;

export type MatterStatus = keyof typeof matterStatuses;
export type MatterPriority = keyof typeof matterPriorities;
export type ArchiveState = keyof typeof archiveStates;

export type MatterInput = {
  referenceNumber: string;
  title: string;
  description: string;
  primaryClientId: number;
  status: MatterStatus;
  priority: MatterPriority;
  openedDate: string;
  targetCloseDate: string | null;
};

export type Matter = MatterInput & {
  id: number;
  primaryClientName: string;
  archivedAtUtc: string | null;
  createdAtUtc: string;
  updatedAtUtc: string;
};

const calendarFormatter = new Intl.DateTimeFormat("en", { dateStyle: "medium", timeZone: "UTC" });

export function formatCalendarDate(value: string) {
  return calendarFormatter.format(new Date(`${value}T00:00:00Z`));
}
