-- ==============================================================================
-- Run this migration in the Supabase SQL Editor to enable Row-Level Security
-- permissions for job proof photos and work orders.
-- ==============================================================================

-- 1. Ensure table exists
create table if not exists public.job_proof_media (
  id uuid not null default gen_random_uuid() primary key,
  work_order_id uuid not null references public.work_orders(id) on delete cascade,
  uploaded_by_id uuid not null references public.users(id),
  proof_type text not null default 'BEFORE_PHOTO',
  media_url text not null,
  latitude numeric,
  longitude numeric,
  created_at timestamptz default now()
);

-- 2. Create index for performance
create index if not exists idx_job_proof_media_work_order_id on public.job_proof_media(work_order_id);
create index if not exists idx_job_proof_media_uploaded_by_id on public.job_proof_media(uploaded_by_id);

-- 3. Enable RLS
alter table public.job_proof_media enable row level security;

-- 4. Allow authenticated users to view job proof media
drop policy if exists job_proof_media_select on public.job_proof_media;
create policy job_proof_media_select on public.job_proof_media
  for select to authenticated, anon
  using (true);

-- 5. Allow users (tenants, contractors, landlords) to insert job proof media
drop policy if exists job_proof_media_insert on public.job_proof_media;
create policy job_proof_media_insert on public.job_proof_media
  for insert to authenticated, anon
  with check (true);

-- 6. Grant table permissions
grant select, insert, update, delete on public.job_proof_media to authenticated, anon;
