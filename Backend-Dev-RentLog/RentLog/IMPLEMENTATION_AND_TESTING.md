# Rent Log — Backend audit and testing guide

## Changes made in this pass

### Maintenance issue service (`Services/Sql/SqlIssueService.cs`)

- Added server-side checks for a valid tenant identity, selected active unit, valid severity, title length and description length before a report can be created.
- Added server-side validation for comments and tenant dispute reasons, rather than relying only on Blazor form validation.
- Added backend validation for contractor name, quote work description, filename and quote amount before saving quote attachments.
- Tightened the status workflow so landlords can attach quotes only while a job is acknowledged or in progress, and cannot approve quotes on resolved or disputed issues.
- Tightened the `MarkFixedAsync` transition so it only accepts acknowledged/in-progress issues and requires meaningful repair notes.
- Added a role guard for issue actions so unexpected roles cannot fall through to the landlord query path.
- Preserved tenant/landlord record scoping and the append-only issue-event history.

### Blazor pages

- Tenant issue submission now shows safe validation messages returned by the backend for expected input/permission failures; unexpected errors are logged and shown generically.
- Tenant and landlord issue-detail actions now surface expected backend validation errors without hiding them behind a generic error message.
- Contractor quote submission displays backend validation messages when the submitted details are invalid.
- Removed an unused no-op dispute-form submit handler. The two explicit dispute actions remain connected to the C# service methods.

### Setup documentation

- Updated `README.md` to make SQL Server the recommended local backend, document the Development demo accounts, explain schema initialization, and list known limitations.

## Manual test checklist

Run in Development using a clean test database or a backup you can restore. The seed routine only seeds when the database has no users.

### A. Tenant report creation

1. Sign in as `alex@example.com` / `password123`.
2. Open **Report an issue**.
3. Submit an empty title or short description; the form should show validation and not save a record.
4. Submit a valid issue with severity `Medium`; verify the success state appears and **View your issue** opens the new report.
5. Confirm the issue appears on the tenant dashboard and landlord issues list.
6. Submit a second report with severity `Emergency`; verify the landlord issue detail displays the emergency banner.
7. If the account has multiple active units, verify the unit selection is honoured; an inactive or unrelated unit must not be accepted by the service.

### B. Landlord workflow

1. Sign out and sign in as `landlord@example.com` / `password123`.
2. Open a new tenant report and click **Acknowledge issue**. Confirm the status changes and history gains an entry.
3. Try attaching a contractor quote with an empty contractor name, zero amount, missing file, unsupported file type, or file over 10 MiB. Invalid submissions must be rejected.
4. Attach a valid PDF/JPG/PNG quote with a positive amount. Verify it appears in the quote list.
5. Approve the quote and verify the contractor name and history are updated.
6. Mark the job fixed with meaningful notes. Verify status becomes **Awaiting confirmation** and the note appears in history.
7. Try to call the mark-fixed operation again after the status changes; it should not apply an invalid transition.

### C. Tenant confirmation and dispute

1. Sign back in as the tenant who reported the issue.
2. Confirm the repair. Verify status becomes **Resolved** and the history records the confirmation.
3. For a separate issue, dispute the repair with a reason. Verify status becomes **Disputed** and the reason is recorded.
4. Sign in as the landlord and respond to the dispute using **Continue work**. Verify the status returns to **In progress**.
5. Repeat on a separate disputed issue using **Mark fixed for confirmation**. Verify it returns to **Awaiting confirmation**.

### D. Comments and access isolation

1. Add a tenant comment and a landlord reply. Both should appear in the issue history with the author and timestamp.
2. Try opening another tenant's issue URL while signed in as a tenant; the issue must not be disclosed.
3. Try opening an issue belonging to a different landlord while signed in as a landlord; the issue must not be disclosed.
4. Verify resolved issues cannot be confirmed a second time and invalid status changes show a retry/refresh message.

### E. Media and resilience

1. Upload a supported photo and, separately, a supported short video within the documented size limits.
2. Try an unsupported file type and a file above the size limit; verify the UI blocks it.
3. Open an issue detail and confirm media loads from the SQL media route.
4. Stop SQL Server or temporarily use an invalid connection string. The UI should show a safe error, and detailed exception information should be written to application logs rather than exposed to the user.

### F. Non-issue buttons

- Test logout on tenant and landlord layouts.
- Test the theme toggle and media gallery open/close/next/previous controls.
- Test landlord ledger **Clear filters** and reminder buttons. Reminder actions only persist a timestamp/count; they do not send a message.
- Test quote download/open and the dashboard navigation links.

## Validation status

The source was reviewed and edited, but this execution environment does not have the .NET SDK available. Therefore `dotnet restore`, `dotnet build`, and runtime/database integration tests could not be executed here. Run these locally before merging:

```powershell
dotnet restore
dotnet build
 dotnet run
```



## Landlord issue actions: quote, mark fixed, and comments

The landlord issue detail page now routes the contractor quote, repair-completion, and comment forms through explicit `OnSubmit` handlers. Each handler validates the form model before calling the C# issue service. This makes validation failures visible instead of silently depending on the form's `OnValidSubmit` event.

After a successful action, the page displays a success message and refreshes the issue model/timeline from the SQL Server service. Quote submission validates the required contractor, positive amount, work description, and selected PDF/JPG/PNG attachment (maximum 10 MB). The SQL service persists the quote file bytes and adds a `Quote` event to the issue history. Marking fixed requires repair details and creates both a comment/history event and the `AwaitingConfirmation` status transition. Landlord comments are stored as `Comment` events and appear in the issue timeline.

### Manual regression checklist

1. Sign in as `landlord@example.com` with the development demo password `password123`. Open an issue in `Acknowledged` or `InProgress` status.
2. Add a contractor name, amount greater than zero, work description, and a PDF/JPG/PNG file under 10 MB. Submit the quote. Confirm a success message, the quote amount and contractor appear, the attachment opens, and a `Contractor quote` event appears in Issue history.
3. Click **Mark fixed**, enter at least five characters describing the repair, and submit. Confirm the status changes to `AwaitingConfirmation` and the repair note appears in Issue history.
4. Enter a landlord comment and select **Post comment**. Confirm the comment appears in Issue history with the landlord's name and role.
5. Try submitting the quote without a file, with an amount of zero, or with a missing contractor name. Confirm validation or a visible error appears and no quote is saved.
6. Sign in as the tenant and confirm the new comment, quote history, and awaiting-confirmation status are visible on the same issue.



## Account registration and password recovery

- Added `/register` for tenant/landlord account creation. Email addresses are normalized, duplicate addresses are rejected, and passwords are stored as ASP.NET Core Identity password hashes (never plaintext). New accounts are redirected to sign-in.
- Replaced the placeholder `/forgot-password` page with a password-reset request form and added `/reset-password` to accept a time-limited, single-use token.
- Password reset tokens are generated with a cryptographically secure random generator; only SHA-256 hashes are stored in SQL Server. Tokens expire after 30 minutes and are invalidated after use or when another reset is completed.
- The SQL backend initializes the `PasswordResetTokens` table for both new databases and existing databases created before this feature. Existing account data is preserved.
- Configure email delivery under `Smtp` in `appsettings.json` or via environment/secret configuration: `Smtp:Host`, `Smtp:Port`, `Smtp:EnableSsl`, `Smtp:From`, and optionally `Smtp:Username` / `Smtp:Password`. Do not commit real SMTP credentials. When running in Development without SMTP configured, the reset link is written to the application log for local testing only. Production must have SMTP configured or recovery emails will not be delivered.
- The request confirmation is deliberately generic so the page does not reveal whether an email address is registered. Account creation currently does not require email verification.

### Registration and recovery manual checks

1. Visit `/register`, create a Tenant account with a valid email and password of at least 8 characters. Confirm it redirects to sign-in and the new account can log in.
2. Attempt to register the same email again; confirm the duplicate is rejected. Test mismatched confirmation and passwords shorter than 8 characters.
3. Configure a test SMTP mailbox, request a reset at `/forgot-password`, and follow the email link. Enter and confirm a new password; verify the old password no longer works and the new one does.
4. Verify expired, modified, and already-used reset links are rejected. Request a second link and confirm the most recently completed reset invalidates outstanding tokens.
5. For local Development without SMTP, request recovery and find the reset URL in the app logs. Do not use this log-based fallback in production.

