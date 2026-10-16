# Rent Log

Rent Log is a mobile-first rental record app built with Blazor and Supabase.
Tenant and landlord authentication, issue history, evidence storage, rent
records, and property data use the Supabase C# client and user-scoped access
tokens. Issue history is append-only. The `register`, `invite`, and forgot
password pages remain placeholders; create accounts in Supabase Auth for now.

## Project Status & Limitations Disclaimer

> **Please Note:** This application is currently a **work-in-progress** and is **not fully functional**. 
> 
> * **The Contractor Portal is Incomplete:** Most notably, the contractor side of things—including the temporary password/PIN verification login flow, contractor quote submissions, job proof media uploads, and invoice approval workflows—is currently stubbed or partially implemented. Core tenant and landlord views are functional, but full end-to-end contractor workflows require further development.

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
3. **Contractor Portal (`/contractor`)** *(Under Construction)*
   - Specialized login flow intended for temporary job access via secure PINs/passwords.
   - Job execution tracking, quote estimation, and invoice submissions.

---

## Tech Stack

- **Framework:** .NET 10 / Blazor Web App
- **Styling:** Tailwind CSS v4
- **Database:** PostgreSQL (Supabase) via EF Core / Direct Client
- **Authentication:** Role-Based Access Control (`TENANT`, `LANDLORD`, `CONTRACTOR`)

---

## Getting Started

### Prerequisites
- [.NET 10 SDK](https://dotnet.microsoft.com/download) installed on your machine.
- An existing PostgreSQL / Supabase database matching the required 13-table schema (`users`, `units`, `work_orders`, etc.).

### Installation & Running Locally

1. **Clone the repository:**
   ```bash
   git clone [https://github.com/your-username/your-property-app.git](https://github.com/your-username/your-property-app.git)
   cd your-property-app
   dotnet build
   dotnet run
