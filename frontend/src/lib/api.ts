const apiBaseUrl = (
  process.env.NEXT_PUBLIC_API_BASE_URL ?? "http://localhost:5109/api/v1"
).replace(/\/+$/, "");

export type FieldErrors = Record<string, string[]>;
export type ApiPath = `/${string}`;

export function apiPath(path: ApiPath, params: Record<string, string | number>): ApiPath {
  const search = new URLSearchParams();
  for (const [name, value] of Object.entries(params)) {
    if (value !== "") search.set(name, String(value));
  }
  return `${path}?${search}`;
}

export function jsonRequest(method: "POST" | "PUT", value: object): RequestInit {
  return {
    method,
    headers: { "Content-Type": "application/json" },
    body: JSON.stringify(value),
  };
}

export class ApiError extends Error {
  constructor(
    public readonly status: number,
    public readonly fieldErrors: FieldErrors = {},
  ) {
    super(`API request failed with status ${status}.`);
    this.name = "ApiError";
  }
}

export class SessionExpiredError extends ApiError {
  constructor(status: number) {
    super(status);
    this.name = "SessionExpiredError";
  }
}

type ApiOptions = RequestInit & { adminToken?: string };

export async function fetchJson<T>(
  path: ApiPath,
  { adminToken, ...options }: ApiOptions = {},
): Promise<T> {
  const headers = new Headers(options.headers);
  headers.set("Accept", "application/json");
  if (adminToken) {
    headers.set("Authorization", `Bearer ${adminToken}`);
  }

  const response = await fetch(`${apiBaseUrl}${path}`, {
    ...options,
    headers,
    cache: "no-store",
    credentials: "omit",
    redirect: "error",
  });

  if (response.status === 401 && adminToken) {
    throw new SessionExpiredError(response.status);
  }
  if (!response.ok) {
    const fieldErrors: FieldErrors = {};
    if (response.status === 400) {
      const problem = (await response.json().catch(() => null)) as {
        errors?: Record<string, unknown>;
      } | null;

      for (const [field, messages] of Object.entries(problem?.errors ?? {})) {
        if (Array.isArray(messages)) {
          fieldErrors[field.toLowerCase()] = messages.filter(
            (message): message is string => typeof message === "string",
          );
        }
      }
    }
    throw new ApiError(response.status, fieldErrors);
  }

  return (await response.json()) as T;
}
