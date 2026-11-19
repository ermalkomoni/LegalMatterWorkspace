"use client";

import { useEffect, useState } from "react";
import { fetchJson } from "@/lib/api";

export function useLinkToken() {
  const [link, setLink] = useState<{ token: string | null; revision: number }>();

  useEffect(() => {
    function readFragment() {
      const [value, ...duplicates] = new URLSearchParams(window.location.hash.slice(1)).getAll("token");
      const token = value && duplicates.length === 0 && /^[a-f0-9]{64}$/.test(value) ? value : null;
      setLink((previous) => previous?.token === token ? previous : { token, revision: (previous?.revision ?? 0) + 1 });
    }

    readFragment();
    window.addEventListener("hashchange", readFragment);
    return () => window.removeEventListener("hashchange", readFragment);
  }, []);

  return link;
}

export function requestWithLink<T>(path: `/external/${string}`, token: string, options: RequestInit = {}) {
  const headers = new Headers(options.headers);
  headers.delete("Authorization");
  headers.set("X-Link-Token", token);
  return fetchJson<T>(path, { ...options, headers, referrerPolicy: "no-referrer" });
}
