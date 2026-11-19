import { defineConfig, devices } from "@playwright/test";

const frontendUrl = new URL(process.env.E2E_FRONTEND_URL ?? "http://localhost:3000");
const apiBaseUrl = process.env.E2E_API_BASE_URL ?? "http://localhost:5109/api/v1";

export default defineConfig({
  testDir: "./tests/e2e",
  timeout: 60_000,
  workers: 1,
  use: { baseURL: frontendUrl.origin },
  projects: [{ name: "chromium", use: devices["Desktop Chrome"] }],
  webServer: process.env.E2E_USE_EXISTING_FRONTEND === "1" ? undefined : {
    command: `npm run dev -- --webpack --hostname localhost --port ${Number(frontendUrl.port || 80)}`,
    url: new URL("/login", frontendUrl).href,
    env: { NEXT_PUBLIC_API_BASE_URL: apiBaseUrl },
    timeout: 180_000,
  },
});
