# Rent Log

Rent Log is a mobile-first rental maintenance and rent-record application built with .NET 9, interactive Blazor Server components and a selectable SQL Server or Supabase backend.

## Recommended local setup: SQL Server

The uploaded project is configured to use SQL Server in Development through `appsettings.Development.json`:

```json
"ConnectionStrings": {
  "SqlServer": "Server=.;Database=RentLogApp;Trusted_Connection=True;TrustServerCertificate=True"
}
```

1. Install the **.NET 9 SDK** and SQL Server (LocalDB or a SQL Server instance). Ensure the instance name in the connection string matches your installation.
2. Open a terminal in the `RentLog` project folder.
3. If your SQL Server instance is not the default instance, update `ConnectionStrings:SqlServer` in `appsettings.Development.json`. Credentials are kept out of source control.
4. Run the app:

   ```powershell
   dotnet restore
   dotnet run
   ```

5. On first startup against an empty database, the Development-only initializer creates the tables and seeds sample accounts and data. Sign in with one of these accounts:

   | Role | Email | Password |
   |---|---|---|
   | Landlord | `landlord@example.com` | `password123` |
   | Tenant | `tenant@example.com` | `password123` |
   | Tenant | `alex@example.com` | `password123` |
   | Tenant | `sam@example.com` | `password123` |
   | Tenant | `morgan@example.com` | `password123` |

   These are **development demo credentials only**. Do not use them in production.

The SQL backend currently uses EF Core `EnsureCreatedAsync`; it creates a new database schema but does not migrate an existing schema. For a pre-existing database, back it up and use an explicit migration plan rather than expecting `EnsureCreatedAsync` to alter existing tables. Demo data is seeded only when the app runs in Development and the `Users` table is empty.

## Supabase backend (optional)

If no SQL Server connection string is configured, the application can use Supabase when both `Supabase:Url` and `Supabase:AnonKey` are configured. In Development, if neither backend is configured, it falls back to mock/demo services. Never use a Supabase `service_role` or secret key in application configuration. Use a publishable/anon key and enforce access through RLS. The existing SQL Server path is the recommended configuration for this project as requested.

## Backend behaviour

- Tenant issue reports are checked against the tenant's active tenancy and saved with an initial status-history event.
- Landlords can only retrieve and update issues assigned to their landlord account.
- Tenants can only retrieve and update their own issues.
- Status changes and comments are written to an append-only issue history.
- Evidence uploads are checked for supported file type and size before being saved.
- Contractor quote uploads accept PDF, JPG or PNG up to 10 MiB and are linked to the issue.
- Development login failures are temporarily locked after repeated failures.
- Rent reminders persist the reminder timestamp and count. They do **not** send email or SMS because no delivery provider is configured.

## Current limitations to be aware of

- Registration/invite acceptance and password recovery are still informational placeholder pages; accounts must be seeded in Development or created through an administrative database workflow.
- The SQL media endpoint uses unguessable GUID media URLs as bearer links rather than cookie-authenticated media authorization. Treat evidence links as sensitive. Before production deployment, replace this with authenticated/short-lived media access and review the overall authentication/session design.
- Contractor assignment is represented by the approved quote/contractor name; a separate contractor login and assignment workflow is not implemented.
- Payments, EFT, DebiCheck, prepaid token generation and real-time push/email/SMS notifications require external providers and are not implemented by the current local SQL backend.
- Production deployment should use EF Core migrations, managed secrets, HTTPS, backups, monitoring and a security review.

## Supabase schema references

- `Schema/Supabase.sql` contains the Supabase schema and RLS policies.
- `Schema/RentLogDB.sql` is an older SQL Server/context schema draft; the active SQL Server model is defined in `Services/Sql/SqlDb.cs` and initialized by `Services/Sql/SqlBackend.cs`.
