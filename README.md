# Rent Log

Rent Log is a mobile-first rental record app built with Blazor and Supabase.
Tenant, landlord, and contractor workflows use the Supabase C# client and
user-scoped access tokens. Issue history is append-only. The `register`,
`invite`, and forgot password pages remain placeholders; create accounts in
Supabase Auth for now.

## Project Status & Limitations Disclaimer

The contractor portal is functional. Contractors can sign in with a job PIN or
temporary password, review assigned work orders, update job status, submit
quotes, upload job proof media, and submit invoices. Contractor account
registration is not available in the app; contractor profiles and access PINs
must be set up through the configured backend.

---

## Features & Portals

The application is structured around three core user roles:

1. **Tenant Portal (`/tenant`)**
   - View active tenancy details, lease terms, and unit information.
   - Track rent and utility payment history and status.
   - Log maintenance requests (work orders) and monitor their progress.
2. **Landlord / Owner Portal (`/landlord`)**
   - Manage properties (`units`) and active tenancies.
   - Monitor financial dashboards, incoming rent, and track operations.
   - Review and approve maintenance work orders and quotes.
3. **Contractor Portal (`/contractor`)**
   - Sign in with a job PIN or temporary access password.
   - View assigned work orders and update their execution status.
   - Submit quotes, upload job proof media, and submit invoices for review.

---

## Tech Stack

- **Framework:** .NET 10 / Blazor Web App
- **Styling:** Tailwind CSS v4
- **Database:** PostgreSQL (Supabase) via the Supabase C# client
- **Authentication:** Role-Based Access Control (`TENANT`, `LANDLORD`, `CONTRACTOR`)

---

## Getting Started

### Prerequisites
- [.NET 10 SDK](https://dotnet.microsoft.com/download) installed on your machine.
- An existing PostgreSQL / Supabase database matching the required 13-table schema (`users`, `units`, `work_orders`, etc.).

### Installation & Running Locally

1. **Clone the repository:**
   ```bash
   git clone (https://github.com/Vlichakane05/RentLog.git)
   cd RentLog

   ```
2. **Build and Run**
   ```bash
   dotnet build
   dotnet run
   ```
  
