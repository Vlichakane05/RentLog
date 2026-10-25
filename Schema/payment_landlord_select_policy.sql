-- Allow landlords to read payment records for tenancies in their own units.
-- The SECURITY DEFINER helper avoids requiring broad access to tenancy/unit rows.

create or replace function public.can_current_user_view_tenancy_payments(target_tenancy_id uuid)
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
        join public.users account on account.id = auth.uid()
        where tenancy.id = target_tenancy_id
          and unit.landlord_id = auth.uid()
          and lower(account.role::text) = 'landlord'
    );
$$;

revoke all on function public.can_current_user_view_tenancy_payments(uuid) from public, anon;
grant execute on function public.can_current_user_view_tenancy_payments(uuid) to authenticated;
grant select on public.payments to authenticated;

drop policy if exists "Landlords view payments for own properties" on public.payments;
create policy "Landlords view payments for own properties" on public.payments
    for select to authenticated
    using (public.can_current_user_view_tenancy_payments(tenancy_id));
