-- Link units and work orders to the existing buildings table.
-- This matches buildings(buildingid integer, landlordid uuid, name, address).
-- Run in the Supabase SQL Editor as the project owner.

create table if not exists public.buildings (
    buildingid integer generated always as identity primary key,
    landlordid uuid not null references public.users(id),
    name varchar not null,
    address varchar
);

alter table public.buildings add column if not exists created_at timestamptz not null default now();

do $$
begin
    if exists (
        select 1 from information_schema.columns
        where table_schema = 'public' and table_name = 'buildings' and column_name = 'landlord_id'
    ) and not exists (
        select 1 from information_schema.columns
        where table_schema = 'public' and table_name = 'buildings' and column_name = 'landlordid'
    ) then
        alter table public.buildings rename column landlord_id to landlordid;
    end if;

    if exists (
        select 1 from information_schema.columns
        where table_schema = 'public' and table_name = 'units'
          and column_name = 'building_id' and data_type <> 'integer'
    ) then
        alter table public.units drop constraint if exists units_building_id_fkey;
        alter table public.units rename column building_id to legacy_building_id;
    end if;

    if exists (
        select 1 from information_schema.columns
        where table_schema = 'public' and table_name = 'work_orders'
          and column_name = 'building_id' and data_type <> 'integer'
    ) then
        alter table public.work_orders drop constraint if exists work_orders_building_id_fkey;
        alter table public.work_orders rename column building_id to legacy_building_id;
    end if;
end;
$$;

alter table public.units add column if not exists building_id integer;
alter table public.work_orders add column if not exists building_id integer;

insert into public.buildings (landlordid, name, address)
select legacy.landlord_id, legacy.name, legacy.address
from (
    select distinct
        landlord_id,
        coalesce(nullif(trim(building_name), ''), 'Existing Building') as name,
        coalesce(nullif(trim(street_address), ''), 'Address not set') as address
    from public.units
) as legacy
where not exists (
    select 1
    from public.buildings building
    where building.landlordid = legacy.landlord_id
      and lower(trim(building.name)) = lower(legacy.name)
      and lower(trim(coalesce(building.address, ''))) = lower(legacy.address)
);

update public.units as unit
set building_id = (
    select candidate.buildingid
    from public.buildings candidate
    where candidate.landlordid = unit.landlord_id
      and lower(trim(candidate.name)) =
          lower(coalesce(nullif(trim(unit.building_name), ''), 'Existing Building'))
      and lower(trim(coalesce(candidate.address, ''))) =
          lower(coalesce(nullif(trim(unit.street_address), ''), 'Address not set'))
    order by candidate.buildingid
    limit 1
)
where unit.building_id is null;

update public.units as unit
set landlord_id = building.landlordid,
    building_name = building.name,
    street_address = coalesce(building.address, '')
from public.buildings as building
where unit.building_id = building.buildingid;

update public.work_orders as work_order
set building_id = unit.building_id
from public.units as unit
where work_order.unit_id = unit.id
  and work_order.building_id is distinct from unit.building_id;

alter table public.units drop column if exists legacy_building_id;
alter table public.work_orders drop column if exists legacy_building_id;

do $$
begin
    if exists (select 1 from public.units where building_id is null) then
        raise exception 'Some units could not be linked to a building.';
    end if;
    if exists (select 1 from public.work_orders where building_id is null) then
        raise exception 'Some work orders could not be linked to a building.';
    end if;

    if not exists (
        select 1 from pg_constraint
        where conrelid = 'public.units'::regclass
          and conname = 'units_building_id_fkey'
    ) then
        alter table public.units
            add constraint units_building_id_fkey
            foreign key (building_id) references public.buildings(buildingid);
    end if;

    if not exists (
        select 1 from pg_constraint
        where conrelid = 'public.work_orders'::regclass
          and conname = 'work_orders_building_id_fkey'
    ) then
        alter table public.work_orders
            add constraint work_orders_building_id_fkey
            foreign key (building_id) references public.buildings(buildingid);
    end if;
end;
$$;

alter table public.units alter column building_id set not null;
alter table public.work_orders alter column building_id set not null;
create index if not exists buildings_landlordid_idx on public.buildings(landlordid);
create index if not exists units_building_id_idx on public.units(building_id);
create index if not exists work_orders_building_id_idx on public.work_orders(building_id);
create index if not exists work_orders_logged_by_id_idx on public.work_orders(logged_by_id);

create or replace function public.sync_unit_building()
returns trigger
language plpgsql
security definer
set search_path = ''
as $$
declare
    building_owner uuid;
    building_name text;
    building_address text;
begin
    select landlordid, name, address
    into building_owner, building_name, building_address
    from public.buildings
    where buildingid = new.building_id;

    if not found then
        raise exception 'The selected building does not exist.';
    end if;

    new.landlord_id := building_owner;
    new.building_name := building_name;
    new.street_address := coalesce(building_address, '');
    return new;
end;
$$;

drop trigger if exists units_sync_building on public.units;
create trigger units_sync_building
    before insert or update of building_id on public.units
    for each row execute function public.sync_unit_building();

create or replace function public.sync_work_order_building()
returns trigger
language plpgsql
security definer
set search_path = ''
as $$
declare
    unit_building_id integer;
begin
    select building_id into unit_building_id
    from public.units
    where id = new.unit_id;

    if not found then
        raise exception 'The selected work order unit does not exist.';
    end if;

    new.building_id := unit_building_id;
    return new;
end;
$$;

drop trigger if exists work_orders_sync_building on public.work_orders;
create trigger work_orders_sync_building
    before insert or update of unit_id, building_id on public.work_orders
    for each row execute function public.sync_work_order_building();

do $$
declare
    existing_policy record;
begin
    for existing_policy in
        select schemaname, tablename, policyname
        from pg_policies
        where schemaname = 'public'
          and tablename in ('buildings', 'units', 'work_orders')
    loop
        execute format(
            'drop policy %I on %I.%I',
            existing_policy.policyname,
            existing_policy.schemaname,
            existing_policy.tablename
        );
    end loop;
end;
$$;

alter table public.buildings enable row level security;
alter table public.units enable row level security;
alter table public.work_orders enable row level security;
grant select, insert, update, delete on public.buildings, public.units, public.work_orders to authenticated;
grant usage, select on sequence public.buildings_buildingid_seq to authenticated;

create or replace function public.is_current_user_landlord()
returns boolean
language sql stable security definer
set search_path = ''
as $$
    select exists (
        select 1 from public.users account
        where account.id = auth.uid()
          and lower(account.role::text) = 'landlord'
    );
$$;

create or replace function public.is_current_user_tenant()
returns boolean
language sql stable security definer
set search_path = ''
as $$
    select exists (
        select 1 from public.users account
        where account.id = auth.uid()
          and lower(account.role::text) = 'tenant'
    );
$$;

create or replace function public.is_building_landlord(target_building_id integer)
returns boolean
language sql stable security definer
set search_path = ''
as $$
    select exists (
        select 1 from public.buildings building
        where building.buildingid = target_building_id
          and building.landlordid = auth.uid()
    );
$$;

create or replace function public.has_active_unit_tenancy(target_unit_id uuid)
returns boolean
language sql stable security definer
set search_path = ''
as $$
    select exists (
        select 1 from public.tenancies tenancy
        where tenancy.unit_id = target_unit_id
          and tenancy.is_active
          and (
              tenancy.primary_tenant_id = auth.uid()
              or exists (
                  select 1 from public.tenancy_co_tenants co_tenant
                  where co_tenant.tenancy_id = tenancy.id
                    and co_tenant.tenant_id = auth.uid()
              )
          )
    );
$$;

create or replace function public.has_active_building_tenancy(target_building_id integer)
returns boolean
language sql stable security definer
set search_path = ''
as $$
    select exists (
        select 1
        from public.units unit
        join public.tenancies tenancy on tenancy.unit_id = unit.id
        where unit.building_id = target_building_id
          and tenancy.is_active
          and (
              tenancy.primary_tenant_id = auth.uid()
              or exists (
                  select 1 from public.tenancy_co_tenants co_tenant
                  where co_tenant.tenancy_id = tenancy.id
                    and co_tenant.tenant_id = auth.uid()
              )
          )
    );
$$;

create or replace function public.is_assigned_work_order_contractor(target_work_order_id uuid)
returns boolean
language sql stable security definer
set search_path = ''
as $$
    select exists (
        select 1
        from public.work_orders work_order
        join public.contractor_profiles contractor
          on contractor.id = work_order.assigned_contractor_id
        join public.users account on account.id = contractor.user_id
        where work_order.id = target_work_order_id
          and contractor.user_id = auth.uid()
          and lower(account.role::text) = 'contractor'
    );
$$;

create or replace function public.is_work_order_for_building_tenant(target_work_order_id uuid)
returns boolean
language sql stable security definer
set search_path = ''
as $$
    select exists (
        select 1
        from public.work_orders work_order
        join public.units unit on unit.id = work_order.unit_id
        join public.tenancies tenancy on tenancy.unit_id = unit.id
        where work_order.id = target_work_order_id
          and work_order.building_id = unit.building_id
          and (
              tenancy.primary_tenant_id = work_order.logged_by_id
              or exists (
                  select 1 from public.tenancy_co_tenants co_tenant
                  where co_tenant.tenancy_id = tenancy.id
                    and co_tenant.tenant_id = work_order.logged_by_id
              )
          )
    );
$$;

revoke all on function public.is_current_user_landlord() from public, anon;
revoke all on function public.is_current_user_tenant() from public, anon;
revoke all on function public.is_building_landlord(integer) from public, anon;
revoke all on function public.has_active_unit_tenancy(uuid) from public, anon;
revoke all on function public.has_active_building_tenancy(integer) from public, anon;
revoke all on function public.is_assigned_work_order_contractor(uuid) from public, anon;
revoke all on function public.is_work_order_for_building_tenant(uuid) from public, anon;
grant execute on function public.is_current_user_landlord() to authenticated;
grant execute on function public.is_current_user_tenant() to authenticated;
grant execute on function public.is_building_landlord(integer) to authenticated;
grant execute on function public.has_active_unit_tenancy(uuid) to authenticated;
grant execute on function public.has_active_building_tenancy(integer) to authenticated;
grant execute on function public.is_assigned_work_order_contractor(uuid) to authenticated;
grant execute on function public.is_work_order_for_building_tenant(uuid) to authenticated;

create policy buildings_select_owned_or_tenanted on public.buildings
    for select to authenticated
    using (public.is_building_landlord(buildingid) or public.has_active_building_tenancy(buildingid));
create policy buildings_insert_owner on public.buildings
    for insert to authenticated
    with check (landlordid = auth.uid() and public.is_current_user_landlord());
create policy buildings_update_owner on public.buildings
    for update to authenticated
    using (public.is_building_landlord(buildingid))
    with check (public.is_building_landlord(buildingid));
create policy buildings_delete_owner on public.buildings
    for delete to authenticated
    using (public.is_building_landlord(buildingid));

create policy units_select_owner_or_tenant on public.units
    for select to authenticated
    using (landlord_id = auth.uid() or public.has_active_unit_tenancy(id));
create policy units_insert_owner on public.units
    for insert to authenticated
    with check (public.is_building_landlord(building_id) and landlord_id = auth.uid());
create policy units_update_owner on public.units
    for update to authenticated
    using (landlord_id = auth.uid())
    with check (public.is_building_landlord(building_id) and landlord_id = auth.uid());
create policy units_delete_owner on public.units
    for delete to authenticated
    using (landlord_id = auth.uid());

create policy work_orders_select_participant on public.work_orders
    for select to authenticated
    using (
        (logged_by_id = auth.uid() and public.is_current_user_tenant())
        or (
            public.is_building_landlord(building_id)
            and public.is_work_order_for_building_tenant(id)
        )
        or public.is_assigned_work_order_contractor(id)
    );
create policy work_orders_insert_tenant on public.work_orders
    for insert to authenticated
    with check (logged_by_id = auth.uid() and public.has_active_unit_tenancy(unit_id));
create policy work_orders_update_landlord_or_contractor on public.work_orders
    for update to authenticated
    using (
        (
            public.is_building_landlord(building_id)
            and public.is_work_order_for_building_tenant(id)
        )
        or public.is_assigned_work_order_contractor(id)
    )
    with check (
        (
            public.is_building_landlord(building_id)
            and public.is_work_order_for_building_tenant(id)
        )
        or public.is_assigned_work_order_contractor(id)
    );
