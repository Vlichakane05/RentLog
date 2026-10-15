# Rent Log

Rent Log is a mobile-first rental record app. The current stage includes mock
sign-in, role-protected tenant and landlord areas, responsive role navigation,
and a persistent light/dark theme toggle. Tenant pages include a rent summary,
issue reporting with photo/video evidence, issue history with comments and
resolution actions, and month-by-month rent history, all backed by mock services.

## Demo accounts

| Role | Email | Password |
| --- | --- | --- |
| Tenant | tenant@example.com | password123 |
| Landlord | landlord@example.com | password123 |

Use `/register` or `/invite` to view the registration/invitation placeholder.
Password recovery is also a placeholder until account services are connected.

Authentication is currently backed by an in-memory mock service. Replace it
with the Supabase implementation before production use. Demo tenant reports use
unit 3B and mock rent records.