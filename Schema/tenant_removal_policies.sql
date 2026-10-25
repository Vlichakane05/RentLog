-- Allow landlords to end a tenancy or remove a co-tenant only for their own units.
-- This migration assumes RLS is already enabled on both tenancy tables.

create or replace function public.is_landlord_of_tenancy(target_tenancy_id uuid)
returns boolean
language sql
stable
security definer
set search_path = ''
as $$
    select exists (
        select 1
        from public.tenancies tenancy
        join public.units unit on unit.id = tenancy.unit_id
        where tenancy.id = target_tenancy_id
          and unit.landlord_id = auth.uid()
    );
$$;

revoke all on function public.is_landlord_of_tenancy(uuid) from public, anon;
grant execute on function public.is_landlord_of_tenancy(uuid) to authenticated;

grant update on public.tenancies to authenticated;
grant delete on public.tenancy_co_tenants to authenticated;

drop policy if exists tenancies_landlord_end_own on public.tenancies;
create policy tenancies_landlord_end_own on public.tenancies
    for update to authenticated
    using (public.is_landlord_of_tenancy(id))
    with check (public.is_landlord_of_tenancy(id));

drop policy if exists tenancy_co_tenants_landlord_remove_own on public.tenancy_co_tenants;
create policy tenancy_co_tenants_landlord_remove_own on public.tenancy_co_tenants
    for delete to authenticated
    using (public.is_landlord_of_tenancy(tenancy_id));
