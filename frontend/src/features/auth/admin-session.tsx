"use client";

import {
  createContext,
  useCallback,
  useContext,
  useEffect,
  useState,
  type ReactNode,
} from "react";
import { ApiError, fetchJson, SessionExpiredError } from "@/lib/api";

const sessionKey = "amanet.adminSession";
const expiredNotice = "Your session ended. Sign in again to continue.";

type AdminIdentity = {
  id: number;
  email: string;
  role: string;
};

type SavedSession = { accessToken: string; expiresAt: number };
type LoginResponse = { accessToken: string; expiresAtUtc: string; admin: AdminIdentity };
type Credentials = { email: string; password: string };

type Session =
  | { status: "checking" }
  | { status: "signed-out"; notice?: string }
  | { status: "unavailable" }
  | { status: "signed-in"; admin: AdminIdentity; expiresAt: number };

type AdminSessionContextValue = {
  session: Session;
  signIn: (credentials: Credentials) => Promise<void>;
  signOut: () => void;
  retry: () => void;
};

const AdminSessionContext = createContext<AdminSessionContextValue | null>(null);

function clearSavedSession() {
  try {
    sessionStorage.removeItem(sessionKey);
  } catch {
    // The browser can disable storage for this origin.
  }
}

function readSavedSession(): SavedSession | null {
  try {
    const saved = sessionStorage.getItem(sessionKey);
    if (!saved) return null;

    const session: Partial<SavedSession> | null = JSON.parse(saved);
    if (
      session &&
      typeof session.accessToken === "string" &&
      session.accessToken &&
      typeof session.expiresAt === "number" &&
      Number.isFinite(session.expiresAt)
    ) {
      return { accessToken: session.accessToken, expiresAt: session.expiresAt };
    }
  } catch {
    // Malformed or inaccessible storage cannot restore an Admin session.
  }
  clearSavedSession();
  return null;
}

export function AdminSessionProvider({ children }: { children: ReactNode }) {
  const [session, setSession] = useState<Session>({ status: "checking" });
  const [restoreAttempt, setRestoreAttempt] = useState(0);

  const endSession = useCallback((notice?: string) => {
    clearSavedSession();
    setSession({ status: "signed-out", notice });
  }, []);

  useEffect(() => {
    const controller = new AbortController();
    const { signal } = controller;

    async function restoreSession() {
      const saved = readSavedSession();
      if (!saved) {
        setSession({ status: "signed-out" });
        return;
      }
      if (saved.expiresAt <= Date.now()) {
        endSession(expiredNotice);
        return;
      }

      try {
        const admin = await fetchJson<AdminIdentity>("/auth/me", {
          adminToken: saved.accessToken,
          signal,
        });
        if (signal.aborted) return;
        if (saved.expiresAt <= Date.now()) {
          endSession(expiredNotice);
          return;
        }
        setSession({ status: "signed-in", admin, expiresAt: saved.expiresAt });
      } catch (error) {
        if (signal.aborted) return;
        if (error instanceof SessionExpiredError || (error instanceof ApiError && error.status === 403)) {
          endSession(expiredNotice);
          return;
        }
        setSession({ status: "unavailable" });
      }
    }

    void restoreSession();
    return () => controller.abort();
  }, [endSession, restoreAttempt]);

  const expiresAt = session.status === "signed-in" ? session.expiresAt : null;
  useEffect(() => {
    if (expiresAt === null) return;

    function checkExpiry() {
      if (expiresAt !== null && expiresAt <= Date.now()) {
        endSession(expiredNotice);
      }
    }

    checkExpiry();
    const timeout = window.setTimeout(checkExpiry, Math.max(0, expiresAt - Date.now()));
    window.addEventListener("pageshow", checkExpiry);
    document.addEventListener("visibilitychange", checkExpiry);
    return () => {
      window.clearTimeout(timeout);
      window.removeEventListener("pageshow", checkExpiry);
      document.removeEventListener("visibilitychange", checkExpiry);
    };
  }, [endSession, expiresAt]);

  async function signIn(credentials: Credentials) {
    const result = await fetchJson<LoginResponse>("/auth/login", {
      method: "POST",
      headers: { "Content-Type": "application/json" },
      body: JSON.stringify(credentials),
    });

    const expiresAt = Date.parse(result.expiresAtUtc);
    if (!result.accessToken || !Number.isFinite(expiresAt) || expiresAt <= Date.now()) {
      throw new Error("The API did not start an active session.");
    }

    const saved: SavedSession = { accessToken: result.accessToken, expiresAt };
    sessionStorage.setItem(sessionKey, JSON.stringify(saved));
    setSession({ status: "signed-in", admin: result.admin, expiresAt });
  }

  function retry() {
    setSession({ status: "checking" });
    setRestoreAttempt((attempt) => attempt + 1);
  }

  return (
    <AdminSessionContext.Provider value={{ session, signIn, signOut: () => endSession(), retry }}>
      {children}
    </AdminSessionContext.Provider>
  );
}

export function useAdminSession() {
  const context = useContext(AdminSessionContext);
  if (!context) {
    throw new Error("An Admin session provider is required.");
  }
  return context;
}
