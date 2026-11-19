import { expect, type Page } from "@playwright/test";
import { adminCredentials, fictionalPdf, test, uploadLink } from "./fixtures";

async function signInWithKeyboard(page: Page) {
  const { email, password } = adminCredentials();
  await expect(page.getByRole("heading", { name: "Sign in" })).toBeVisible();
  const emailInput = page.getByLabel("Email", { exact: true });
  const passwordInput = page.getByLabel("Password", { exact: true });
  await page.keyboard.press("Tab");
  await expect(emailInput).toBeFocused();
  await page.keyboard.insertText(email);
  await page.keyboard.press("Tab");
  await expect(passwordInput).toBeFocused();
  await page.keyboard.insertText(password);
  await page.keyboard.press("Tab");
  await expect(page.getByRole("button", { name: "Sign in" })).toBeFocused();
  await page.keyboard.press("Enter");
  await expect(page.getByRole("navigation", { name: "Workspace" })).toBeVisible();
}

async function expectNoHorizontalOverflow(page: Page) {
  await expect.poll(() => page.evaluate(() => document.documentElement.scrollWidth <= window.innerWidth)).toBe(true);
}

test("protects Admin pages and restores the session on reload", async ({ page }) => {
  await page.goto("/notifications");
  await expect(page).toHaveURL(/\/login$/);
  await expect(page.getByRole("navigation", { name: "Workspace" })).toBeHidden();
  await signInWithKeyboard(page);
  await page.getByRole("button", { name: "Notifications", exact: true }).click();
  await page.getByRole("link", { name: "View all notifications" }).click();
  await expect(page.getByRole("heading", { name: "Notifications", exact: true })).toBeVisible();

  const identity = page.waitForResponse((response) => new URL(response.url()).pathname.endsWith("/auth/me"));
  await page.reload();
  expect((await identity).ok()).toBe(true);
  await expect(page.getByRole("heading", { name: "Notifications", exact: true })).toBeVisible();
  await page.getByRole("button", { name: "Account", exact: true }).click();
  await page.getByRole("button", { name: "Sign out" }).click();
  await expect(page).toHaveURL(/\/login$/);
  await page.goto("/notifications");
  await expect(page).toHaveURL(/\/login$/);
  await expect(page.getByRole("heading", { name: "Notifications", exact: true })).toBeHidden();
});

test("accepts an emailed public PDF upload and clears its pending notification", async ({ page, browser, uploadInvite }) => {
  const viewport = { width: 360, height: 800 };
  await page.setViewportSize(viewport);
  await page.goto("/login");
  await expectNoHorizontalOverflow(page);
  await signInWithKeyboard(page);
  await expectNoHorizontalOverflow(page);

  await page.getByRole("searchbox", { name: "Search matters" }).fill(uploadInvite.referenceNumber);
  const result = page.getByRole("row").filter({ hasText: uploadInvite.referenceNumber });
  await expect(result).toBeVisible();
  // On phones the matter name spans the card, including when its title wraps.
  const cell = result.getByRole("cell").first();
  await expect.poll(() => cell.evaluate((element) => element.clientWidth)).toBeGreaterThan(viewport.width * 0.65);
  await page.getByRole("button", { name: "Filters", exact: true }).click();
  await page.getByLabel("Status", { exact: true }).selectOption("Closed");
  await expect(page.getByText("No matters match these filters.")).toBeVisible();
  await page.getByLabel("Status", { exact: true }).selectOption("Open");
  await expect(result).toBeVisible();

  const link = await uploadLink(uploadInvite);
  expect(link.origin, "Mailpit link targets the configured frontend").toBe(new URL(page.url()).origin);
  const signedOut = await browser.newContext({ viewport });
  try {
    const recipient = await signedOut.newPage();
    await recipient.goto(`${link.origin}${link.pathname}`);
    await expect(recipient.getByRole("heading", { name: "Upload link unavailable" })).toBeVisible();
    await recipient.evaluate((fragment) => { window.location.hash = fragment; }, link.hash);
    await expect(recipient.getByRole("heading", { name: uploadInvite.title })).toBeVisible();
    await expect(recipient.getByRole("navigation", { name: "Workspace" })).toBeHidden();
    await expectNoHorizontalOverflow(recipient);

    const file = recipient.getByLabel("PDF file");
    await recipient.keyboard.press("Tab");
    await expect(file).toBeFocused();
    await file.setInputFiles({ name: "fictional-client-document.pdf", mimeType: "application/pdf", buffer: fictionalPdf() });
    const uploaded = recipient.waitForResponse((response) => response.request().method() === "POST" &&
      new URL(response.url()).pathname.endsWith(link.pathname));
    await recipient.keyboard.press("Tab");
    await expect(recipient.getByRole("button", { name: "Submit PDF" })).toBeFocused();
    await recipient.keyboard.press("Enter");
    await expect(recipient.getByText("Your PDF was received and is awaiting review.")).toBeVisible();
    const submission = (await uploaded).request();
    const headers = await submission.allHeaders();
    expect(headers["x-link-token"] === new URLSearchParams(link.hash.slice(1)).get("token"), "Uses the scoped link header").toBe(true);
    expect(headers.authorization).toBeUndefined();
    expect(new URL(submission.url()).search).toBe("");
    await expectNoHorizontalOverflow(recipient);
  } finally {
    await signedOut.close();
  }

  await page.getByRole("button", { name: "Notifications", exact: true }).click();
  await page.getByRole("link", { name: "View all notifications" }).click();
  const pending = page.getByRole("region", { name: "Pending actions" }).getByRole("link", { name: `Submission awaiting review: ${uploadInvite.title}` });
  const history = page.getByRole("region", { name: "Activity" }).getByRole("link", { name: `PDF submitted for review: ${uploadInvite.title} (version 1).` });
  await expect(pending).toBeVisible();
  await page.getByRole("button", { name: "Activity", exact: true }).click();
  await expect(history).toBeVisible();
  await page.getByRole("button", { name: "Pending actions", exact: true }).click();
  await expectNoHorizontalOverflow(page);
  await pending.click();
  await expect(page).toHaveURL(new RegExp(`/matters/${uploadInvite.matterId}/requests/${uploadInvite.id}(?:[?#]|$)`));
  await page.getByRole("button", { name: "Accept submission" }).click();
  await expect(page.getByText("Submission accepted.")).toBeVisible();
  await page.getByRole("main").getByRole("link", { name: "Notifications", exact: true }).click();
  await expect(page.getByRole("region", { name: "Pending actions" })).toBeVisible();
  await expect(pending).toBeHidden();
  await page.getByRole("button", { name: "Activity", exact: true }).click();
  await expect(history).toBeVisible();
});
