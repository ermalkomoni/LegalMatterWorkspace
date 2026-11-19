# Amanet frontend implementation prompts

Run one numbered prompt at a time. To start a feature, ask the implementer to read this file, apply the shared instructions, and implement only the named prompt. Each **Review** is a stopping point. Report the changes, checks run, and anything the reviewer needs to verify. Wait for the owner to request the next feature. **Never commit or push. The owner reviews, commits, and pushes manually.**

## Shared instructions

- Build the workflows in the [assignment PDF](Amanet_Fullstack_Engineer_Task.pdf) against the existing `/api/v1` API. Use the [backend README](README.md), controllers, and DTOs for exact routes, request fields, and responses. API paths below are relative to `/api/v1`. Use fictional matters and local Mailpit for review.
- Use Next.js App Router, TypeScript, Tailwind CSS, and built-in `fetch`. Keep form and interaction state in React. After a successful write, refresh the affected record or list. This keeps data flow understandable without a cache library or global store.
- Give the workspace its own identity using white surfaces, near-black type, compact sans-serif headings, and a restrained red primary action. Make statuses readable in words and distinguish destructive actions by label. Keep the public forms focused on the recipient's one task. [Amanet brand reference](https://www.amanets.com/)
- Build responsive layouts as each screen is added. Use labeled fields, keyboard-accessible controls, visible focus, and feedback for loading, empty results, validation, success, and failure. Retain entered values after recoverable errors. Include a desktop and narrow-screen check in each UI review.

## 1. Scaffold the project

Create a runnable Next.js app in `frontend/` using npm, TypeScript, and Tailwind. Keep this feature to scaffolding. Use a minimal root page to confirm startup. Document the folder layout briefly in the frontend README and create implementation files as their features arrive.

| Folder | Responsibility |
| --- | --- |
| `src/app/` | Routes and layouts. Group Admin pages under `(admin)` and public links under `external/`. |
| `src/features/` | Domain-specific forms, views, and API calls grouped by feature. |
| `src/components/` | UI shared by multiple features. |
| `src/lib/` | Shared configuration and request helpers as needed. |

Add `frontend/.env.example` with the public local setting `NEXT_PUBLIC_API_BASE_URL=http://localhost:5109/api/v1`. Keep local env files, dependencies, and build output ignored while keeping the example and npm lockfile trackable. Backend credentials and signing keys belong in the existing backend configuration. Ensure scaffolding tools cannot initialize a nested repository or create a commit.

**Review.** Install from the lockfile, start on port 3000, open the minimal page, and run a production build. Verify Git ignores `frontend/.env.local` and `frontend/.next/` and can track the example. Stop before implementing authentication or workspace UI.

## 2. Admin sign-in and workspace layout

Build a labeled sign-in form using `POST /auth/login`. Restore the Admin identity on reload with `GET /auth/me`. For this assessment, keep the bearer token in tab-scoped session storage, clear it on sign-out or expiry, and attach it only to Admin API calls. Keep credentials and tokens out of logs and URLs. Add a small fetch helper for the configured API URL, validation errors, and expired sessions. Keep public link routes independent of Admin sign-in.

Create the responsive Admin layout with a workspace heading, signed-in identity, and sign-out. Add navigation destinations as their screens become available.

**Review.** Try a failed login, sign in successfully, reload a protected page, then sign out and revisit it. Verify private content is hidden when signed out.

## 3. Clients and matters

Make the matter list the Admin landing page. Use `/clients` and `/matters` for paginated lists, search, creation, details, and editing. Provide the existing matter filters for client, status, priority, and archive state. Show reference, title, client, status, priority, and key dates where they help identify a matter. Support archive and restore. Keep archived matters readable and disable edits. Place duplicate-reference and invalid-date feedback beside the relevant fields.

**Review.** Create a fictional client and matter, find the matter by search and filter, update it, then archive and restore it. Clear the filters to recover from an empty result.

## 4. Matter tasks, notes, deadlines, and activity

Extend the matter detail page using its nested `tasks`, `notes`, `deadlines`, and `activities` endpoints. Support creating, editing, and deleting tasks, notes, and deadlines. Support task completion and reopening, plus deadline completion. Use the API's overdue flag. Keep internal notes in the Admin workspace. Show the paginated, dated matter history and refresh it after changes. Preserve business dates as calendar dates when formatting them.

**Review.** Add a note and a past-due task and deadline. Complete both, reopen the task, and verify their states and dated activity. Reopen the matter after archiving it and confirm its history remains readable.

## 5. PDF library and version history

Build a searchable firm-wide library using `GET /documents`. Reuse it within a matter via `?matterId=…`. Use the nested matter document routes for uploading a PDF, editing its title or category, listing versions, and uploading a new version where permitted. Preview or download a selected version by fetching its authenticated file as a blob. Use the version metadata for the filename, release temporary object URLs, and offer a download when inline preview is unavailable. File routes take a **version number**. Review and signing actions in later features take a **version ID**.

**Review.** Upload two distinct PDFs as versions of one document. Search for it from both views, preview the original, and download the newer version. Try an invalid file and a file over the 20 MiB upload limit.

## 6. Client document requests, uploads, and review

From a matter, create an external request with action `Upload`, recipient details, instructions, and expiry. Use its request list and detail to show outstanding work, delivery attempts, and submission history. Support resend, reminders, and revocation through the existing request actions. Read the latest delivery attempt before reporting email success. Reuse these controls for signing invitations in Feature 7.

Build the public page at exactly `/external/upload/[requestId]`, matching the emailed link. Read its `#token=…` fragment in the browser and send it only in the `X-Link-Token` header to the scoped API. Load the context, show instructions or the correction explanation, and submit one PDF as multipart field `file` when `canSubmit` is true. Show the returned receipt or an unavailable-link message as appropriate.

Let the Admin preview the submitted version and accept it or request corrections with an explanation. Submit the decision to `/matters/{matterId}/external-requests/{requestId}/submissions/{versionId}/review`. A correction triggers its own follow-up email.

**Review.** Follow the invitation in Mailpit from a signed-out browser. Upload, request a correction, follow the new email, resubmit, and accept. Verify both versions remain visible and the superseded link is unusable.

## 7. Admin and external PDF signing

Offer two signing paths for an eligible PDF, including an accepted client submission. For Admin signing, preview the exact source version, display the backend's fixed `SigningConsent.Text`, collect a typed name and explicit consent, and post to the document's `/sign` endpoint. For external signing, create a matter request with action `Sign` and the selected `documentVersionId`. Show delivery and signing progress separately.

Build `/external/sign/[requestId]` using the fragment and header handling from Feature 6. Fetch the scoped context and PDF. Post `/viewed` after displaying the source and `/started` when the recipient begins signing. Display the returned consent text and submit the typed name and checked consent. On completion, offer the signed PDF. In the matter, show the saved signer, time, and final version from the document's `/signature` result. Respect the one-signer rule and handle unavailable links.

**Review.** Have the Admin sign an accepted client upload. Use a second document to exercise the emailed link through Sent, Viewed, Started, and Signed. Reopen the completed record, download the signed PDF, and verify the original is still available in version history.

## 8. Admin notifications and assessment handoff

Add the Admin notification view using paginated `GET /notifications`. Show document responses, signature activity, and outstanding actions. Use the returned matter and resource IDs to take the Admin to the relevant request, task, or deadline. Refresh after returning from an action so resolved work leaves the pending list. Some live items have a null notification ID.

Finish the assessment handoff in the README. Include frontend setup, the public API URL, demo Admin provisioning, and a repeatable way to populate fictional sample matters through existing endpoints. Provide a short walkthrough using Mailpit and document actual limitations. Add a small set of repeatable frontend workflow tests covering an Admin session and a public upload or signing flow. Use disposable fixtures.

**Review.** From a fresh local setup, follow the documented demo, resolve a pending notification, and verify the update. Run the tests and production build. Check keyboard navigation and narrow-screen usability from sign-in through a completed public submission.
