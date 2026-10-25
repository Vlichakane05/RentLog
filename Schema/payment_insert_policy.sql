-- Let an authenticated tenant record a payment only for their own active tenancy.
-- The SECURITY DEFINER helper checks tenancy membership without relying on
-- tenants having direct read access to another tenancy's rows.

create or replace function public.can_current_user_record_payment(target_tenancy_id uuid)
returns boolean
language sql
stable
security definer
set search_path = ''
as $$
    select exists (
        select 1
        from public.tenancies tenancy
        join public.users account on account.id = auth.uid()
        where tenancy.id = target_tenancy_id
          and tenancy.is_active
          and lower(account.role::text) = 'tenant'
          and (
              tenancy.primary_tenant_id = auth.uid()
              or exists (
                  select 1
                  from public.tenancy_co_tenants co_tenant
                  where co_tenant.tenancy_id = tenancy.id
                    and co_tenant.tenant_id = auth.uid()
              )
          )
    );
$$;

revoke all on function public.can_current_user_record_payment(uuid) from public, anon;
grant execute on function public.can_current_user_record_payment(uuid) to authenticated;
grant insert on public.payments to authenticated;

drop policy if exists "Tenants record own active tenancy payments" on public.payments;
create policy "Tenants record own active tenancy payments" on public.payments
    for insert to authenticated
    with check (
        payer_id = auth.uid()
        and public.can_current_user_record_payment(tenancy_id)
        and amount > 0
        and status = 'SETTLED'
        and gateway_reference is null
    );
