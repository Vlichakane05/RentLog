# Rent Log

Rent Log is a mobile-first rental record app built with Blazor and Supabase.
Tenant and landlord authentication, issue history, evidence storage, rent
records, and property data use the Supabase C# client and user-scoped access
tokens. Issue history is append-only. The `register`, `invite`, and forgot
password pages remain placeholders; create accounts in Supabase Auth for now.

## Supabase setup

1. Create a Supabase project and copy the **Project URL** and its
   **publishable key** (or legacy anon key) from **Project Settings → API**.
   Never use the `service_role` or secret key in this app.
2. In **SQL Editor**, run [`Schema/Supabase.sql`](Schema/Supabase.sql). It
   creates the application tables, profile trigger, RLS policies, immutable
   issue-event enforcement, and the private `issue-media` bucket with upload
   limits and Storage policies. `Schema/RentLogDB.sql` is an older context-only
   draft and is not the Stage 7 migration.
3. Create accounts in **Authentication → Users**. A database trigger creates a
   `public.users` profile with the `tenant` role. To promote an account to
   landlord, run this as the project administrator in SQL Editor:

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