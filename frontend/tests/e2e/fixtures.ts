import { randomUUID } from "node:crypto";
import { expect, test as base, type APIRequestContext, type APIResponse } from "@playwright/test";

export function adminCredentials() {
  const email = process.env.E2E_ADMIN_EMAIL;
  const password = process.env.E2E_ADMIN_PASSWORD;
  if (!email || !password) throw new Error("Set E2E_ADMIN_EMAIL and E2E_ADMIN_PASSWORD for the fixture API.");
  return { email, password };
}

async function readJson<T>(response: APIResponse): Promise<T> {
  if (!response.ok()) throw new Error(`Fixture request to ${new URL(response.url()).pathname} returned HTTP ${response.status()}.`);
  return response.json() as Promise<T>;
}

async function createUploadInvite(api: APIRequestContext, matterId: number, suffix: string, recipientEmail: string) {
  const title = `Browser test document ${suffix}`;
  const result = await readJson<{ id: string; deliveryAttempts: { status: string }[] }>(
    await api.post(`matters/${matterId}/external-requests`, {
      data: { action: "Upload", title, recipientEmail, recipientName: "Riley Example", instructions: "Upload the fictional fixture PDF." },
    }),
  );
  expect(result.deliveryAttempts[0]?.status, "Mailpit delivery").toBe("Sent");
  return { id: result.id, title, matterId, recipientEmail };
}

type UploadInvite = Awaited<ReturnType<typeof createUploadInvite>> & { referenceNumber: string };

export const test = base.extend<{ uploadInvite: UploadInvite }>({
  async uploadInvite({ playwright }, use) {
    const apiUrl = `${(process.env.E2E_API_BASE_URL ?? "http://localhost:5109/api/v1").replace(/\/+$/, "")}/`;
    const anonymous = await playwright.request.newContext({ baseURL: apiUrl });
    let token: string;
    try {
      const session = await readJson<{ accessToken: string }>(await anonymous.post("auth/login", { data: adminCredentials() }));
      token = session.accessToken;
    } finally {
      await anonymous.dispose();
    }

    const api = await playwright.request.newContext({ baseURL: apiUrl, extraHTTPHeaders: { Authorization: `Bearer ${token}` } });
    let matterId: number | undefined;
    try {
      const suffix = randomUUID().slice(0, 8);
      const recipientEmail = `browser-${suffix}@example.test`;
      const client = await readJson<{ id: number }>(await api.post("clients", {
        data: { name: `Riley Example ${suffix}`, email: recipientEmail },
      }));
      const referenceNumber = `E2E-${suffix}`;
      const matter = await readJson<{ id: number }>(await api.post("matters", {
        data: {
          primaryClientId: client.id, referenceNumber, title: `Fictional review ${suffix}`,
          description: "Temporary browser test matter.", status: "Open", priority: "Normal",
          openedDate: new Date().toISOString().slice(0, 10),
        },
      }));
      matterId = matter.id;
      const invite = await createUploadInvite(api, matterId, suffix, recipientEmail);
      await use({ ...invite, referenceNumber });
    } finally {
      try {
        if (matterId) await readJson(await api.post(`matters/${matterId}/archive`));
      } finally {
        await api.dispose();
      }
    }
  },
});

export async function uploadLink(invite: UploadInvite) {
  const mailpit = new URL(process.env.E2E_MAILPIT_URL ?? "http://localhost:8025");
  const path = `/external/upload/${invite.id}`;
  const response = await fetch(new URL(`/api/v1/search?query=${encodeURIComponent(`to:${invite.recipientEmail}`)}`, mailpit));
  if (!response.ok) throw new Error(`Mailpit message search returned HTTP ${response.status}.`);
  const { messages } = await response.json() as { messages: { ID: string }[] };
  const messageId = messages[0]?.ID;
  if (!messageId) throw new Error("Mailpit did not contain the fixture's upload invitation.");
  const message = await fetch(new URL(`/api/v1/message/${encodeURIComponent(messageId)}`, mailpit));
  if (!message.ok) throw new Error(`Mailpit message lookup returned HTTP ${message.status}.`);
  const { Text } = await message.json() as { Text: string };
  const match = Text.match(new RegExp(`https?://[^\\s]+${path}#token=[a-f0-9]{64}`));
  if (!match) throw new Error("The fixture invitation did not contain the expected upload link.");
  return new URL(match[0]);
}

export function fictionalPdf() {
  const stream = "BT /F1 18 Tf 40 720 Td (Fictional client document) Tj ET\n";
  const objects = [
    "<< /Type /Catalog /Pages 2 0 R >>",
    "<< /Type /Pages /Count 1 /Kids [3 0 R] >>",
    "<< /Type /Page /Parent 2 0 R /MediaBox [0 0 612 792] /Resources << /Font << /F1 4 0 R >> >> /Contents 5 0 R >>",
    "<< /Type /Font /Subtype /Type1 /BaseFont /Helvetica >>",
    `<< /Length ${stream.length} >>\nstream\n${stream}endstream`,
  ];
  let pdf = "%PDF-1.4\n";
  const offsets = objects.map((object, index) => {
    const offset = pdf.length;
    pdf += `${index + 1} 0 obj\n${object}\nendobj\n`;
    return offset;
  });
  const rows = offsets.map((offset) => `${String(offset).padStart(10, "0")} 00000 n \n`).join("");
  return Buffer.from(`${pdf}xref\n0 6\n0000000000 65535 f \n${rows}trailer\n<< /Size 6 /Root 1 0 R >>\nstartxref\n${pdf.length}\n%%EOF\n`);
}
