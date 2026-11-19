"use client";

import { useEffect, useState } from "react";
import type { ApiPath } from "@/lib/api";
import { useAdminSession } from "./admin-session";

export function useAdminQuery<T>(path: ApiPath | null) {
  const { request } = useAdminSession();
  const [revision, setRevision] = useState(0);
  const [result, setResult] = useState<{
    path: ApiPath;
    revision: number;
    data?: T;
    error?: Error;
  }>();

  useEffect(() => {
    if (!path) return;
    const controller = new AbortController();
    const { signal } = controller;
    void request<T>(path, { signal }).then(
      (data) => {
        if (!signal.aborted) setResult({ path, revision, data });
      },
      (error: unknown) => {
        if (!signal.aborted) {
          setResult((previous) => ({
            path,
            revision,
            error: error instanceof Error ? error : new Error("The request failed."),
            data: previous?.path === path ? previous.data : undefined,
          }));
        }
      },
    );
    return () => controller.abort();
  }, [path, request, revision]);

  const current = result?.path === path ? result : undefined;
  const pending = path !== null && current?.revision !== revision;

  function refresh(data?: T) {
    if (path && data !== undefined) setResult({ path, revision, data });
    setRevision((value) => value + 1);
  }

  return { data: current?.data, error: pending ? undefined : current?.error, pending, refresh };
}
