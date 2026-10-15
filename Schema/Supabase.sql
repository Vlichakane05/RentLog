-- Rent Log Supabase schema. Run in the Supabase SQL Editor as the project owner.
create extension if not exists pgcrypto;

create table if not exists public.users (
    id uuid primary key references auth.users(id) on delete cascade,
    email text not null unique,
    full_name text not null,
    role text not null default 'tenant' check (role in ('tenant', 'landlord')),
    created_at timestamptz not null default now()
);

create table if not exists public.properties (
    id uuid primary key default gen_random_uuid(),
    landlord_id uuid not null references public.users(id),
    name text not null,
    address text not null,
    created_at timestamptz not null default now()
);

create table if not exists public.units (
    id uuid primary key default gen_random_uuid(),
    property_id uuid not null references public.properties(id) on delete cascade,
    unit_number text not null,
    monthly_rent numeric(12, 2) not null check (monthly_rent >= 0),
    created_at timestamptz not null default now(),
    unique (property_id, unit_number)
);

create table if not exists public.tenancies (
    id uuid primary key default gen_random_uuid(),
    unit_id uuid not null references public.units(id),
    tenant_id uuid not null references public.users(id),
    start_date date not null,
    end_date date,
    is_active boolean not null default true,
    created_at timestamptz not null default now(),
    check (end_date is null or end_date >= start_date)
);

create unique index if not exists tenancies_one_active_per_unit
    on public.tenancies(unit_id) where is_active;
create index if not exists tenancies_by_tenant on public.tenancies(tenant_id, is_active);

create table if not exists public.issues (
    id uuid primary key default gen_random_uuid(),
    property_id uuid not null references public.properties(id),
    unit_id uuid not null references public.units(id),
    tenant_id uuid not null references public.users(id),
    landlord_id uuid not null references public.users(id),
    unit_number text not null,
    title text not null check (char_length(title) between 1 and 100),
    description text not null check (char_length(description) between 1 and 3000),
    severity text not null check (severity in ('low', 'medium', 'high', 'emergency')),
    status text not null default 'open'
        check (status in ('open', 'acknowledged', 'in_progress', 'awaiting_confirmation', 'resolved', 'disputed')),
    is_disputed boolean not null default false,
    contractor_name text,
    created_at timestamptz not null default now()
);

create index if not exists issues_by_tenant on public.issues(tenant_id, created_at desc);
create index if not exists issues_by_landlord on public.issues(landlord_id, created_at desc);

create table if not exists public.issue_events (
    id uuid primary key default gen_random_uuid(),
    issue_id uuid not null references public.issues(id) on delete cascade,
    author_id uuid not null references public.users(id),
    author_name text not null,
    author_role text not null check (author_role in ('Tenant', 'Landlord')),
    event_type text not null check (event_type in ('StatusChange', 'Comment', 'MediaUpload', 'Quote')),
    description text not null,
    new_status text check (new_status is null or new_status in ('open', 'acknowledged', 'in_progress', 'awaiting_confirmation', 'resolved', 'disputed')),
    created_at timestamptz not null default now()
);

create index if not exists issue_events_by_issue on public.issue_events(issue_id, created_at);

create table if not exists public.media (
    id uuid primary key default gen_random_uuid(),
    issue_id uuid not null references public.issues(id) on delete cascade,
    event_id uuid not null references public.issue_events(id) on delete restrict,
    uploaded_by uuid not null references public.users(id),
    storage_path text not null unique,
    file_name text not null,
    mime_type text not null check (mime_type in ('image/jpeg', 'image/png', 'image/heic', 'video/mp4', 'video/quicktime')),
    file_size_bytes bigint not null check (file_size_bytes between 1 and 52428800),
    alt_text text not null default '',
    duration_seconds numeric(8, 2),
    created_at timestamptz not null default now()
);

create index if not exists media_by_issue_event on public.media(issue_id, event_id);

create table if not exists public.issue_quotes (
    id uuid primary key default gen_random_uuid(),
    issue_id uuid not null references public.issues(id) on delete cascade,
    contractor_name text not null,
    amount numeric(12, 2) not null check (amount > 0),
    work_description text not null,
    file_name text not null,
    storage_path text not null unique,
    status text not null default 'pending_approval' check (status in ('pending_approval', 'approved')),
    submitted_by uuid not null references public.users(id),
    submitted_at timestamptz not null default now()
);

create index if not exists issue_quotes_by_issue on public.issue_quotes(issue_id, submitted_at);

create table if not exists public.rent_payments (
    id uuid primary key default gen_random_uuid(),
    tenancy_id uuid not null references public.tenancies(id),
    unit_id uuid not null references public.units(id),
    tenant_id uuid not null references public.users(id),
    period date not null,
    amount numeric(12, 2) not null check (amount >= 0),
    due_date date not null,
    paid_date date,
    status text not null check (status in ('paid', 'due', 'overdue')),
    last_reminder_at timestamptz,
    reminder_count integer not null default 0 check (reminder_count >= 0),
    created_at timestamptz not null default now(),
    unique (tenancy_id, period)
);

create index if not exists rent_payments_by_tenant on public.rent_payments(tenant_id, period desc);
create index if not exists rent_payments_by_unit on public.rent_payments(unit_id, period desc);

create or replace function public.is_landlord_user()
returns boolean
language sql stable security definer
set search_path = ''
as $$
    select exists (
        select 1 from public.users u
        where u.id = auth.uid() and u.role = 'landlord'
    );
$$;

create or replace function public.is_tenant_user()
returns boolean
language sql stable security definer
set search_path = ''
as $$
    select exists (
        select 1 from public.users u
        where u.id = auth.uid() and u.role = 'tenant'
    );
$$;

create or replace function public.is_property_landlord(target_property_id uuid)
returns boolean
language sql stable security definer
set search_path = ''
as $$
    select exists (
        select 1 from public.properties p
        where p.id = target_property_id and p.landlord_id = auth.uid()
    ) and public.is_landlord_user();
$$;

create or replace function public.is_landlord_of_unit(target_unit_id uuid)
returns boolean
language sql stable security definer
set search_path = ''
as $$
    select exists (
        select 1
        from public.units u
        join public.properties p on p.id = u.property_id
        where u.id = target_unit_id and p.landlord_id = auth.uid()
    ) and public.is_landlord_user();
$$;

create or replace function public.can_access_property(target_property_id uuid)
returns boolean
language sql stable security definer
set search_path = ''
as $$
    select public.is_property_landlord(target_property_id)
        or exists (
            select 1
            from public.tenancies t
            join public.units u on u.id = t.unit_id
            where u.property_id = target_property_id
              and t.tenant_id = auth.uid()
              and t.is_active
        );
$$;

create or replace function public.can_access_issue(target_issue_id uuid)
returns boolean
language sql stable security definer
set search_path = ''
as $$
    select exists (
        select 1 from public.issues i
        where i.id = target_issue_id
          and (
              (i.tenant_id = auth.uid() and public.is_tenant_user())
              or (i.landlord_id = auth.uid() and public.is_landlord_user())
          )
    );
$$;

create or replace function public.can_view_user(target_user_id uuid)
returns boolean
language sql stable security definer
set search_path = ''
as $$
    select target_user_id = auth.uid()
        or (public.is_landlord_user() and exists (
            select 1
            from public.tenancies t
            join public.units u on u.id = t.unit_id
            join public.properties p on p.id = u.property_id
            where t.tenant_id = target_user_id
              and t.is_active
              and p.landlord_id = auth.uid()
        ))
        or (public.is_tenant_user() and exists (
            select 1
            from public.tenancies own_tenancy
            join public.tenancies other_tenancy on other_tenancy.unit_id = own_tenancy.unit_id
            where own_tenancy.tenant_id = auth.uid()
              and other_tenancy.tenant_id = target_user_id
              and own_tenancy.is_active
              and other_tenancy.is_active
        ));
$$;

revoke all on function public.is_landlord_user() from public, anon;
revoke all on function public.is_tenant_user() from public, anon;
revoke all on function public.is_property_landlord(uuid) from public, anon;
revoke all on function public.is_landlord_of_unit(uuid) from public, anon;
revoke all on function public.can_access_property(uuid) from public, anon;
revoke all on function public.can_access_issue(uuid) from public, anon;
revoke all on function public.can_view_user(uuid) from public, anon;
grant execute on function public.is_landlord_user() to authenticated;
grant execute on function public.is_tenant_user() to authenticated;
grant execute on function public.is_property_landlord(uuid) to authenticated;
grant execute on function public.is_landlord_of_unit(uuid) to authenticated;
grant execute on function public.can_access_property(uuid) to authenticated;
grant execute on function public.can_access_issue(uuid) to authenticated;
grant execute on function public.can_view_user(uuid) to authenticated;

create or replace function public.create_rent_log_profile()
returns trigger
language plpgsql security definer
set search_path = ''
as $$
begin
    insert into public.users (id, email, full_name, role)
    values (
        new.id,
        coalesce(new.email, ''),
        coalesce(nullif(new.raw_user_meta_data ->> 'full_name', ''), split_part(coalesce(new.email, 'Tenant'), '@', 1)),
        'tenant'
    )
    on conflict (id) do update
        set email = excluded.email;
    return new;
end;
$$;

drop trigger if exists on_auth_user_created_rent_log on auth.users;
create trigger on_auth_user_created_rent_log
    after insert on auth.users
    for each row execute function public.create_rent_log_profile();

create or replace function public.prepare_issue_write()
returns trigger
language plpgsql security definer
set search_path = ''
as $$
declare
    unit_property uuid;
    property_landlord uuid;
    active_tenant uuid;
    resolved_unit_number text;
begin
    if tg_op = 'INSERT' then
        if auth.uid() is null or new.tenant_id <> auth.uid() or not public.is_tenant_user() then
            raise exception 'An issue must be submitted by its active tenant';
        end if;

        select u.property_id, p.landlord_id, t.tenant_id, u.unit_number
        into unit_property, property_landlord, active_tenant, resolved_unit_number
        from public.units u
        join public.properties p on p.id = u.property_id
        join public.tenancies t on t.unit_id = u.id and t.is_active
        where u.id = new.unit_id;

        if active_tenant is null or active_tenant <> auth.uid() then
            raise exception 'The issue unit is not part of an active tenancy for this user';
        end if;

        new.property_id := unit_property;
        new.landlord_id := property_landlord;
        new.unit_number := resolved_unit_number;
        new.status := 'open';
        new.is_disputed := false;
    else
        if new.id <> old.id
            or new.unit_id <> old.unit_id
            or new.property_id <> old.property_id
            or new.tenant_id <> old.tenant_id
            or new.landlord_id <> old.landlord_id
            or new.unit_number <> old.unit_number
            or new.title <> old.title
            or new.description <> old.description
            or new.severity <> old.severity
            or new.created_at <> old.created_at then
            raise exception 'Issue report details are immutable';
        end if;

        if new.status is distinct from old.status then
            if auth.uid() = old.tenant_id then
                if old.status <> 'awaiting_confirmation' or new.status not in ('resolved', 'disputed') then
                    raise exception 'Tenant status transition is not allowed';
                end if;
            elsif auth.uid() = old.landlord_id then
                if not (
                    (old.status = 'open' and new.status = 'acknowledged')
                    or (old.status = 'acknowledged' and new.status in ('in_progress', 'awaiting_confirmation'))
                    or (old.status = 'in_progress' and new.status = 'awaiting_confirmation')
                    or (old.status = 'disputed' and new.status in ('in_progress', 'awaiting_confirmation'))
                ) then
                    raise exception 'Landlord status transition is not allowed';
                end if;
            else
                raise exception 'Only an issue participant can change its status';
            end if;
        end if;

        if new.contractor_name is distinct from old.contractor_name
            and auth.uid() <> old.landlord_id then
            raise exception 'Only the landlord can change contractor details';
        end if;
    end if;

    new.is_disputed := new.status = 'disputed';
    return new;
end;
$$;

drop trigger if exists issues_prepare_write on public.issues;
create trigger issues_prepare_write
    before insert or update on public.issues
    for each row execute function public.prepare_issue_write();

create or replace function public.prepare_issue_event()
returns trigger
language plpgsql security definer
set search_path = ''
as $$
declare
    profile public.users%rowtype;
begin
    if new.author_id <> auth.uid() then
        raise exception 'Issue event author must be the authenticated user';
    end if;

    if new.event_type in ('StatusChange', 'MediaUpload') and pg_trigger_depth() < 2 then
        raise exception 'Status and media events must be recorded by their database operation';
    end if;

    if new.event_type = 'Quote' and not exists (
        select 1
        from public.issue_quotes q
        join public.issues i on i.id = q.issue_id
        where q.issue_id = new.issue_id
          and i.landlord_id = auth.uid()
    ) then
        raise exception 'A quote event must reference a contractor quote for this issue';
    end if;

    if new.event_type <> 'StatusChange' and new.new_status is not null then
        raise exception 'Only status events can include a status value';
    end if;

    select * into profile from public.users where id = auth.uid();
    if profile.id is null then
        raise exception 'A Rent Log profile is required to add issue history';
    end if;

    new.author_name := profile.full_name;
    new.author_role := case profile.role when 'landlord' then 'Landlord' else 'Tenant' end;
    new.created_at := now();
    return new;
end;
$$;

drop trigger if exists issue_events_prepare_insert on public.issue_events;
create trigger issue_events_prepare_insert
    before insert on public.issue_events
    for each row execute function public.prepare_issue_event();

create or replace function public.protect_issue_event_history()
returns trigger
language plpgsql
set search_path = ''
as $$
begin
    raise exception 'Issue history is append-only';
end;
$$;

drop trigger if exists issue_events_immutable on public.issue_events;
create trigger issue_events_immutable
    before update or delete on public.issue_events
    for each row execute function public.protect_issue_event_history();

create or replace function public.record_issue_status_event()
returns trigger
language plpgsql security definer
set search_path = ''
as $$
declare
    actor uuid := auth.uid();
    summary text;
begin
    if tg_op = 'INSERT' then
        insert into public.issue_events (issue_id, author_id, event_type, description, new_status)
        values (new.id, new.tenant_id, 'StatusChange', new.description, 'open');
    elsif new.status is distinct from old.status then
        summary := case new.status
            when 'acknowledged' then 'The landlord acknowledged this issue.'
            when 'in_progress' then 'Work is continuing on this issue.'
            when 'awaiting_confirmation' then 'The landlord marked this issue fixed. Please confirm the repair.'
            when 'resolved' then 'The tenant confirmed the issue is resolved.'
            when 'disputed' then 'The tenant disputed the resolution.'
            else 'The issue status changed.'
        end;
        insert into public.issue_events (issue_id, author_id, event_type, description, new_status)
        values (new.id, actor, 'StatusChange', summary, new.status);
    end if;
    return new;
end;
$$;

drop trigger if exists issues_record_status_event on public.issues;
create trigger issues_record_status_event
    after insert or update of status on public.issues
    for each row execute function public.record_issue_status_event();

create or replace function public.record_issue_quote_event()
returns trigger
language plpgsql
security definer
set search_path = ''
as $$
declare
    description text;
begin
    if tg_op = 'INSERT' then
        description := 'Quote from ' || new.contractor_name || ' for R ' ||
            to_char(new.amount, 'FM999,999,990.00') || ': ' || new.work_description ||
            ' (' || new.file_name || ')';
    else
        update public.issues
        set contractor_name = new.contractor_name,
            status = case when status = 'acknowledged' then 'in_progress' else status end
        where id = new.issue_id;

        description := 'The quote from ' || new.contractor_name || ' for R ' ||
            to_char(new.amount, 'FM999,999,990.00') || ' was approved.';
    end if;

    insert into public.issue_events (issue_id, author_id, event_type, description)
    values (new.issue_id, auth.uid(), 'Quote', description);
    return new;
end;
$$;

drop trigger if exists issue_quotes_record_event on public.issue_quotes;
create trigger issue_quotes_record_event
    after insert or update of status on public.issue_quotes
    for each row execute function public.record_issue_quote_event();

create or replace function public.validate_media_record()
returns trigger
language plpgsql
security definer
set search_path = ''
as $$
declare
    object_metadata jsonb;
    object_size bigint;
    object_mime text;
begin
    if new.uploaded_by <> auth.uid() then
        raise exception 'Media uploader must be the authenticated user';
    end if;
    if new.storage_path not like ('pending/' || auth.uid()::text || '/%') then
        raise exception 'Media must be uploaded by the authenticated user';
    end if;

    select metadata into object_metadata
    from storage.objects
    where bucket_id = 'issue-media' and name = new.storage_path;

    if object_metadata is null then
        raise exception 'The media object must be uploaded before it is attached';
    end if;

    object_size := coalesce(
        nullif(object_metadata ->> 'size', '')::bigint,
        nullif(object_metadata ->> 'contentLength', '')::bigint
    );
    object_mime := object_metadata ->> 'mimetype';
    if object_size is null or object_mime is distinct from new.mime_type then
        raise exception 'Stored media metadata does not match the uploaded object';
    end if;
    if new.file_size_bytes <> object_size then
        raise exception 'Recorded media size does not match the uploaded object';
    end if;

    if new.mime_type in ('image/jpeg', 'image/png', 'image/heic') then
        if object_size > 10485760 or new.duration_seconds is not null then
            raise exception 'Photos must not exceed 10 MB and cannot have a video duration';
        end if;
    end if;
    if new.mime_type in ('video/mp4', 'video/quicktime') then
        if object_size > 52428800 or new.duration_seconds is null or new.duration_seconds > 30 then
            raise exception 'Videos must not exceed 50 MB or 30 seconds';
        end if;
    end if;

    new.file_size_bytes := object_size;
    insert into public.issue_events (issue_id, author_id, event_type, description)
    values (new.issue_id, new.uploaded_by, 'MediaUpload', 'Evidence attached: ' || new.file_name)
    returning id into new.event_id;

    return new;
end;
$$;

create or replace function public.validate_tenancy_write()
returns trigger
language plpgsql
security definer
set search_path = ''
as $$
declare
    tenant_role text;
begin
    if auth.uid() is not null
        and not public.is_property_landlord((select property_id from public.units where id = new.unit_id)) then
        raise exception 'Only the property's landlord can manage its tenancies';
    end if;

    select role into tenant_role from public.users where id = new.tenant_id;
    if tenant_role is distinct from 'tenant' then
        raise exception 'A tenancy must be assigned to a tenant account';
    end if;

    return new;
end;
$$;

create or replace function public.protect_issue_quote()
returns trigger
language plpgsql
set search_path = ''
as $$
begin
    if tg_op = 'DELETE' then
        raise exception 'Contractor quote history is immutable';
    end if;

    if new.id <> old.id
        or new.issue_id <> old.issue_id
        or new.contractor_name <> old.contractor_name
        or new.amount <> old.amount
        or new.work_description <> old.work_description
        or new.file_name <> old.file_name
        or new.storage_path <> old.storage_path
        or new.submitted_by <> old.submitted_by
        or new.submitted_at <> old.submitted_at
        or old.status <> 'pending_approval'
        or new.status <> 'approved' then
        raise exception 'Contractor quote details are immutable';
    end if;
    return new;
end;
$$;

drop trigger if exists issue_quotes_immutable on public.issue_quotes;
create trigger issue_quotes_immutable
    before update or delete on public.issue_quotes
    for each row execute function public.protect_issue_quote();

drop trigger if exists tenancies_validate_write on public.tenancies;
create trigger tenancies_validate_write
    before insert or update on public.tenancies
    for each row execute function public.validate_tenancy_write();

create or replace function public.validate_rent_payment_write()
returns trigger
language plpgsql
security definer
set search_path = ''
as $$
declare
    tenancy_tenant uuid;
    tenancy_unit uuid;
    unit_property uuid;
begin
    select t.tenant_id, t.unit_id, u.property_id
    into tenancy_tenant, tenancy_unit, unit_property
    from public.tenancies t
    join public.units u on u.id = t.unit_id
    where t.id = new.tenancy_id;

    if tenancy_tenant is null
        or tenancy_tenant <> new.tenant_id
        or tenancy_unit <> new.unit_id
        or not exists (select 1 from public.users u where u.id = new.tenant_id and u.role = 'tenant')
        or (auth.uid() is not null and not public.is_property_landlord(unit_property)) then
        raise exception 'Rent payments must match a tenancy managed by the authenticated landlord';
    end if;

    return new;
end;
$$;

drop trigger if exists rent_payments_validate_write on public.rent_payments;
create trigger rent_payments_validate_write
    before insert or update on public.rent_payments
    for each row execute function public.validate_rent_payment_write();

drop trigger if exists media_validate_insert on public.media;
create trigger media_validate_insert
    before insert on public.media
    for each row execute function public.validate_media_record();

alter table public.users enable row level security;
alter table public.properties enable row level security;
alter table public.units enable row level security;
alter table public.tenancies enable row level security;
alter table public.issues enable row level security;
alter table public.issue_events enable row level security;
alter table public.media enable row level security;
alter table public.issue_quotes enable row level security;
alter table public.rent_payments enable row level security;

drop policy if exists users_select_related on public.users;
create policy users_select_related on public.users
    for select to authenticated using (public.can_view_user(id));

drop policy if exists properties_select_related on public.properties;
create policy properties_select_related on public.properties
    for select to authenticated using (public.can_access_property(id));
drop policy if exists properties_landlord_insert on public.properties;
create policy properties_landlord_insert on public.properties
    for insert to authenticated with check (landlord_id = auth.uid() and public.is_landlord_user());
drop policy if exists properties_landlord_update on public.properties;
create policy properties_landlord_update on public.properties
    for update to authenticated
    using (public.is_property_landlord(id))
    with check (landlord_id = auth.uid() and public.is_landlord_user());
drop policy if exists properties_landlord_delete on public.properties;
create policy properties_landlord_delete on public.properties
    for delete to authenticated using (public.is_property_landlord(id));

drop policy if exists units_select_related on public.units;
create policy units_select_related on public.units
    for select to authenticated using (public.can_access_property(property_id));
drop policy if exists units_landlord_write on public.units;
create policy units_landlord_write on public.units
    for all to authenticated
    using (public.is_property_landlord(property_id))
    with check (public.is_property_landlord(property_id));

drop policy if exists tenancies_select_related on public.tenancies;
create policy tenancies_select_related on public.tenancies
    for select to authenticated
    using ((tenant_id = auth.uid() and public.is_tenant_user()) or public.is_landlord_of_unit(unit_id));
drop policy if exists tenancies_landlord_write on public.tenancies;
create policy tenancies_landlord_write on public.tenancies
    for all to authenticated
    using (public.is_landlord_of_unit(unit_id))
    with check (public.is_landlord_of_unit(unit_id));

drop policy if exists issues_select_participants on public.issues;
create policy issues_select_participants on public.issues
    for select to authenticated using (public.can_access_issue(id));
drop policy if exists issues_tenant_insert on public.issues;
create policy issues_tenant_insert on public.issues
    for insert to authenticated
    with check (
        tenant_id = auth.uid()
        and public.is_tenant_user()
        and exists (
            select 1 from public.tenancies t
            where t.unit_id = issues.unit_id
              and t.tenant_id = auth.uid()
              and t.is_active
        )
    );
drop policy if exists issues_participant_update on public.issues;
create policy issues_participant_update on public.issues
    for update to authenticated
    using (public.can_access_issue(id))
    with check (public.can_access_issue(id));

drop policy if exists issue_events_select_participants on public.issue_events;
create policy issue_events_select_participants on public.issue_events
    for select to authenticated using (public.can_access_issue(issue_id));
drop policy if exists issue_events_insert_participants on public.issue_events;
create policy issue_events_insert_participants on public.issue_events
    for insert to authenticated
    with check (author_id = auth.uid() and public.can_access_issue(issue_id));
revoke update, delete, truncate on public.issue_events from anon, authenticated;

drop policy if exists media_select_participants on public.media;
create policy media_select_participants on public.media
    for select to authenticated using (public.can_access_issue(issue_id));
drop policy if exists media_insert_participants on public.media;
create policy media_insert_participants on public.media
    for insert to authenticated
    with check (uploaded_by = auth.uid() and public.can_access_issue(issue_id));

drop policy if exists issue_quotes_select_participants on public.issue_quotes;
create policy issue_quotes_select_participants on public.issue_quotes
    for select to authenticated using (public.can_access_issue(issue_id));
drop policy if exists issue_quotes_landlord_insert on public.issue_quotes;
create policy issue_quotes_landlord_insert on public.issue_quotes
    for insert to authenticated
    with check (
        submitted_by = auth.uid()
        and public.is_landlord_user()
        and public.can_access_issue(issue_id)
    );
drop policy if exists issue_quotes_landlord_update on public.issue_quotes;
create policy issue_quotes_landlord_update on public.issue_quotes
    for update to authenticated
    using (public.is_landlord_user() and public.can_access_issue(issue_id))
    with check (public.is_landlord_user() and public.can_access_issue(issue_id));

drop policy if exists rent_payments_select_participants on public.rent_payments;
create policy rent_payments_select_participants on public.rent_payments
    for select to authenticated
    using (
        tenant_id = auth.uid()
        or exists (
            select 1 from public.units u
            join public.properties p on p.id = u.property_id
            where u.id = unit_id and public.is_property_landlord(p.id)
        )
    );
drop policy if exists rent_payments_landlord_insert on public.rent_payments;
create policy rent_payments_landlord_insert on public.rent_payments
    for insert to authenticated
    with check (public.is_property_landlord((select property_id from public.units where id = unit_id)));
drop policy if exists rent_payments_landlord_update on public.rent_payments;
create policy rent_payments_landlord_update on public.rent_payments
    for update to authenticated
    using (public.is_property_landlord((select property_id from public.units where id = unit_id)))
    with check (public.is_property_landlord((select property_id from public.units where id = unit_id)));
drop policy if exists rent_payments_landlord_delete on public.rent_payments;
create policy rent_payments_landlord_delete on public.rent_payments
    for delete to authenticated
    using (public.is_property_landlord((select property_id from public.units where id = unit_id)));

grant select on public.users, public.properties, public.units, public.tenancies,
    public.issues, public.issue_events, public.media, public.issue_quotes, public.rent_payments
    to authenticated;
grant insert, update, delete on public.properties, public.units, public.tenancies,
    public.issues, public.media, public.issue_quotes, public.rent_payments to authenticated;
grant insert on public.issue_events to authenticated;

insert into storage.buckets (id, name, public, file_size_limit, allowed_mime_types)
values (
    'issue-media',
    'issue-media',
    false,
    52428800,
    array['image/jpeg', 'image/png', 'image/heic', 'video/mp4', 'video/quicktime', 'application/pdf']
)
on conflict (id) do update
set public = excluded.public,
    file_size_limit = excluded.file_size_limit,
    allowed_mime_types = excluded.allowed_mime_types;

drop policy if exists issue_media_upload_owned_pending_or_quote on storage.objects;
create policy issue_media_upload_owned_pending_or_quote on storage.objects
    for insert to authenticated
    with check (
        bucket_id = 'issue-media'
        and (
            (
                (storage.foldername(name))[1] = 'pending'
                and (storage.foldername(name))[2] = auth.uid()::text
                and (
                    (
                        metadata ->> 'mimetype' in ('image/jpeg', 'image/png', 'image/heic')
                        and (metadata ->> 'size')::bigint <= 10485760
                    )
                    or (
                        metadata ->> 'mimetype' in ('video/mp4', 'video/quicktime')
                        and (metadata ->> 'size')::bigint <= 52428800
                    )
                )
            )
            or (
                (storage.foldername(name))[1] = 'quotes'
                and public.can_access_issue(((storage.foldername(name))[2])::uuid)
                and exists (
                    select 1 from public.issues i
                    where i.id = ((storage.foldername(name))[2])::uuid
                      and i.landlord_id = auth.uid()
                )
                and metadata ->> 'mimetype' in ('application/pdf', 'image/jpeg', 'image/png')
                and (metadata ->> 'size')::bigint <= 10485760
            )
        )
    );

drop policy if exists issue_media_read_owned_or_linked on storage.objects;
create policy issue_media_read_owned_or_linked on storage.objects
    for select to authenticated
    using (
        bucket_id = 'issue-media'
        and (
            (
                (storage.foldername(name))[1] = 'pending'
                and (storage.foldername(name))[2] = auth.uid()::text
            )
            or exists (select 1 from public.media m where m.storage_path = name and public.can_access_issue(m.issue_id))
            or exists (
                select 1 from public.issue_quotes q
                where q.storage_path = name and public.can_access_issue(q.issue_id)
            )
        )
    );

drop policy if exists issue_media_update_none on storage.objects;
create policy issue_media_update_none on storage.objects
    for update to authenticated using (false) with check (false);
drop policy if exists issue_media_delete_none on storage.objects;
create policy issue_media_delete_none on storage.objects
    for delete to authenticated using (false);
