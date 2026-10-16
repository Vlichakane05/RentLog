-- Run this in the Supabase SQL editor so landlord/tenant invites persist.
create table if not exists public.tenancy_invites (
  id uuid not null default gen_random_uuid() primary key,
  unit_id uuid not null references public.units(id),
  landlord_id uuid not null references public.users(id),
  tenant_email text not null,
  tenant_id uuid,
  status text not null default 'PENDING',
  start_date date not null,
  end_date date not null,
  created_at timestamptz default now(),
  responded_at timestamptz
);

create index if not exists tenancy_invites_by_email on public.tenancy_invites (lower(tenant_email), status);
create index if not exists tenancy_invites_by_landlord on public.tenancy_invites (landlord_id, status);

alter table public.tenancy_invites enable row level security;

drop policy if exists tenancy_invites_read on public.tenancy_invites;
create policy tenancy_invites_read on public.tenancy_invites
  for select using (true);

drop policy if exists tenancy_invites_write on public.tenancy_invites;
create policy tenancy_invites_write on public.tenancy_invites
  for all using (true) with check (true);

grant select, insert, update, delete on public.tenancy_invites to anon, authenticated;
