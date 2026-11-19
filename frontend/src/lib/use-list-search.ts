"use client";

import { useEffect, useState } from "react";

// Commit search and its page reset together, after the user pauses typing.
export function useListSearch() {
  const [draft, setDraft] = useState("");
  const [query, setQuery] = useState({ search: "", page: 1 });
  useEffect(() => {
    const search = draft.trim();
    const timer = window.setTimeout(() => {
      setQuery((previous) => previous.search === search ? previous : { search, page: 1 });
    }, 220);
    return () => window.clearTimeout(timer);
  }, [draft]);
  function setPage(page: number) {
    setQuery((previous) => ({ ...previous, page }));
  }
  function clear() {
    setDraft("");
    setQuery({ search: "", page: 1 });
  }
  return { draft, setDraft, ...query, setPage, clear };
}
