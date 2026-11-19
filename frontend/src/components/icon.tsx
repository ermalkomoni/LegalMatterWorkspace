const paths = {
  folder: "M3 7a2 2 0 0 1 2-2h5l2 3h7a2 2 0 0 1 2 2v8a2 2 0 0 1-2 2H5a2 2 0 0 1-2-2z",
  users: "M16 21v-2a4 4 0 0 0-4-4H8a4 4 0 0 0-4 4v2 M14 7a4 4 0 1 1-8 0 4 4 0 1 1 8 0 M17 4a4 4 0 0 1 0 7 M20 20v-1a4 4 0 0 0-3-4",
  file: "M6 3h8l4 4v14H6z M14 3v5h4 M9 12h6 M9 16h5",
  bell: "M18 8a6 6 0 0 0-12 0c0 7-3 8-3 9h18c0-1-3-2-3-9 M10 20a2 2 0 0 0 4 0",
  user: "M16 8a4 4 0 1 1-8 0 4 4 0 1 1 8 0 M4.5 20a7.5 7.5 0 0 1 15 0",
  exit: "M10 4H4v16h6 M9 12h12 M17 8l4 4-4 4",
  search: "M16 10a6 6 0 1 1-12 0 6 6 0 1 1 12 0 M15 15l5 5",
  plus: "M12 5v14 M5 12h14",
  next: "m9 5 7 7-7 7",
  clock: "M21 12a9 9 0 1 1-18 0 9 9 0 1 1 18 0 M12 7v5l3 2",
  check: "m4 12 4 4L20 5",
  inbox: "M3 15 6 5h12l3 10v4H3z M3 14h5l2 3h4l2-3h5",
  lock: "M5 10h14v11H5z M8 10V7a4 4 0 1 1 8 0v3 M12 14v3",
  arrow: "M4 12h16m-7-7 7 7-7 7",
  filter: "M3 7h4m4 0h10 M3 17h10m4 0h4 M7 4v6m8 4v6",
  refresh: "M20 10a8 8 0 1 0-1 7 M20 4v6h-6",
} as const;

export function Icon({ name, size = 18 }: { name: keyof typeof paths; size?: number }) {
  return <svg width={size} height={size} viewBox="0 0 24 24" fill="none" stroke="currentColor"
    strokeWidth="1.65" strokeLinecap="round" strokeLinejoin="round" aria-hidden="true" focusable="false">
    <path d={paths[name]} />
  </svg>;
}
