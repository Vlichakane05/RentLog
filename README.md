# Rent Log

Rent Log is a mobile-first rental record app built with Blazor and Supabase.
Tenant, landlord, and contractor authentication, issue history, evidence
storage, rent records, and property data use the Supabase C# client and
user-scoped access tokens. Issue history is append-only. Tenants register from
`/register?token=...`; contractors join from `/contractor/join?token=...`.
Forgot-password support is not yet configured.

## Supabase setup

1. Create a Supabase project and copy the **Project URL** and its
   **publishable key** (or legacy anon key) from **Project Settings → API**.
   Never use the `service_role` or secret key in this app.
2. In **SQL Editor**, run [`Schema/Supabase.sql`](Schema/Supabase.sql), then
   [`Schema/Contractors.sql`](Schema/Contractors.sql). The contractor migration
   is additive and required for both new and existing projects. It creates the
   contractor tables, privacy-filtered job feed, atomic job and invite
   workflows, reassignment history, Storage access rules, and landlord
   notifications. Enable the `pg_cron` extension before running it to schedule
   the hourly completion check for Work done jobs that have gone 7 days without
   tenant verification. Landlord contractor invites grant portfolio access;
   unit invites grant access only to that unit. `Schema/RentLogDB.sql` is an older
   context-only draft and is not the Stage 7 migration.
3. Create tenant and contractor accounts using their invitation links. The
   database validates and consumes each invitation when the account is created;
   if email confirmation is enabled, the invitation is linked before the user
   confirms their email. Landlords must be provisioned by an administrator.
   To promote an account to landlord, run this in SQL Editor:

   ```sql
   update public.users
   set role = 'landlord'
   where email = 'landlord@example.com';
   ```

   Roles are not accepted from sign-up metadata, so a new account cannot grant
   itself landlord access.
4. Add properties, units, active tenancies, and rent-payment rows in the
   dashboard or through an administrative workflow. Link each tenancy to the
   Supabase Auth user's UUID, not an email address.
5. Configure the app's local secrets (from the repository directory):

   ```powershell
   dotnet user-secrets init
   dotnet user-secrets set "Supabase:Url" "https://YOUR_PROJECT_REF.supabase.co"
   dotnet user-secrets set "Supabase:AnonKey" "YOUR_PUBLISHABLE_OR_ANON_KEY"
   dotnet run
   ```

   The equivalent environment variables are `SUPABASE_URL` and
   `SUPABASE_ANON_KEY`. For deployment, configure those as platform secrets.
   The app fails at startup with a clear configuration error if either value is
   missing. Do not commit credentials to `appsettings.json`.

The database and Storage clients use the signed-in user's token; RLS is the
authorization boundary. Media is uploaded to a private bucket and served using
one-hour signed URLs. Storage and database policies enforce a 10 MiB photo
limit and a 50 MiB video limit; the existing browser-side validation enforces
the 30-second video duration limit. Supabase Storage cannot inspect video
duration, so a hostile client that bypasses the app would need a server-side
media processor/Edge Function for that final check. The server's Blazor
message limit is raised to accommodate the current base64 video upload flow.

Landlord reminder actions persist the reminder timestamp and count in
`rent_payments`; they do not send email or SMS because no notification provider
is configured. Session state is held by the scoped Supabase client for the
active Blazor Server circuit; signing in again may be necessary after a full
browser reload. Supabase signed media links expire after one hour and are
renewed when an issue is loaded.

Landlords can manage buildings and units at `/landlord/properties`, issue
multiple single-use tenant links with locally generated QR codes, and manage
their contractor network at `/landlord/contractors`. Contractor invite links
expire after thirty days; unit invite links expire after seven days. Landlords
can end a specific tenant's active tenancy when they move out, then issue fresh
invites without affecting other tenants in that unit.
Deactivating a contractor ends only that landlord's access; if
they hold an approved or in-progress job, reassign the named jobs first.
Landlord alerts are available at `/landlord/alerts`, with New and Earlier
sections based on whether the issue has been opened or actioned. Emergency
issues are prioritized; contractor withdrawals, completed-work updates, and
tenant reopens are shown with their issues. Releasing a job warns when no
active linked contractor matches its trade but still allows the release.
Pending contractor claims can be approved, rejected back to the board, or
reassigned at `/landlord/approvals`; contractor assignment changes are recorded
in `reassignment_log`.
Contractor data access is provided by `IContractorService`; database and
Storage operations remain in the Supabase service layer. Contractors can only
read the sanitized job feed and their accepted jobs. Property and unit
locations are omitted until landlord approval, and tenant identity/contact
fields are never returned by that feed. Contractors use `/contractor` to filter
their emergency-first job board by landlord, review safe job details before
accepting, and manage jobs in status tabs. The street address, unit and
preferred access windows remain hidden until approval. Accepted contractors
can view issue evidence, add notes and completion media, update work status,
and edit their trades and service area; a tenant reopen note appears on the
reopened job.