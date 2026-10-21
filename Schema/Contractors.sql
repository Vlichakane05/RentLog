-- Contractor support migration. Run after Supabase.sql as the project owner.
create table if not exists public.contractors (
    id uuid primary key default gen_random_uuid(),
    auth_user_id uuid not null unique references public.users(id) on delete cascade,
    display_name text not null check (char_length(display_name) between 1 and 120),
    contact_details jsonb not null default '{}'::jsonb check (jsonb_typeof(contact_details) = 'object'),
    service_area text not null default '',
    created_at timestamptz not null default now()
);

create table if not exists public.contractor_trades (
    contractor_id uuid not null references public.contractors(id) on delete cascade,
    category text not null check (char_length(category) between 1 and 80),
    primary key (contractor_id, category)
);

create table if not exists public.landlord_contractors (
    landlord_id uuid not null references public.users(id) on delete cascade,
    contractor_id uuid not null references public.contractors(id) on delete cascade,
    active boolean not null default true,
    all_units boolean not null default true,
    date_added timestamptz not null default now(),
    primary key (landlord_id, contractor_id)
);

alter table public.landlord_contractors
    add column if not exists all_units boolean not null default true;

create table if not exists public.contractor_unit_access (
    landlord_id uuid not null,
    contractor_id uuid not null,
    unit_id uuid not null references public.units(id) on delete cascade,
    date_added timestamptz not null default now(),
    primary key (contractor_id, unit_id),
    foreign key (landlord_id, contractor_id)
        references public.landlord_contractors(landlord_id, contractor_id) on delete cascade
);

create table if not exists public.contractor_invites (
    token text primary key,
    landlord_id uuid not null references public.users(id) on delete cascade,
    expires_at timestamptz not null,
    used boolean not null default false,
    created_at timestamptz not null default now()
);

create table if not exists public.unit_invites (
    token text primary key,
    unit_id uuid not null references public.units(id) on delete cascade,
    expires_at timestamptz not null,
    used boolean not null default false,
    created_at timestamptz not null default now()
);

create index if not exists unit_invites_by_unit on public.unit_invites(unit_id, expires_at);

drop index if exists public.tenancies_one_active_per_unit;
create unique index if not exists tenancies_one_active_unit_per_tenant
    on public.tenancies(unit_id, tenant_id) where is_active;

create table if not exists public.reassignment_log (
    id bigint generated always as identity primary key,
    issue_id uuid not null references public.issues(id) on delete cascade,
    previous_contractor uuid references public.contractors(id) on delete set null,
    new_contractor uuid references public.contractors(id) on delete set null,
    changed_by uuid not null references public.users(id),
    changed_at timestamptz not null default now()
);

create index if not exists reassignment_log_by_issue on public.reassignment_log(issue_id, changed_at);

create table if not exists public.landlord_notifications (
    id uuid primary key default gen_random_uuid(),
    landlord_id uuid not null references public.users(id) on delete cascade,
    issue_id uuid not null references public.issues(id) on delete cascade,
    message text not null,
    created_at timestamptz not null default now(),
    read_at timestamptz
);

create index if not exists landlord_notifications_unread
    on public.landlord_notifications(landlord_id, created_at desc) where read_at is null;

create or replace function public.end_tenant_unit_tenancy(p_unit_id uuid, p_tenant_id uuid)
returns void
language plpgsql security definer
set search_path = ''
as $$
begin
    if not public.is_landlord_user() or not public.is_landlord_of_unit(p_unit_id) then
        raise exception 'Only the landlord of this unit can end a tenancy';
    end if;

    update public.tenancies
    set is_active = false,
        end_date = greatest(current_date, start_date)
    where unit_id = p_unit_id
      and tenant_id = p_tenant_id
      and is_active;
    if not found then
        raise exception 'No active tenancy was found for this tenant and unit';
    end if;
end;
$$;

alter table public.issues
    add column if not exists category text,
    add column if not exists preferred_windows jsonb not null default '[]'::jsonb,
    add column if not exists released_to_contractors boolean not null default false,
    add column if not exists assigned_contractor_id uuid references public.contractors(id) on delete set null,
    add column if not exists contractor_status text not null default 'Reported',
    add column if not exists accepted_at timestamptz,
    add column if not exists landlord_seen_at timestamptz,
    add column if not exists work_done_at timestamptz,
    add column if not exists tenant_verified_at timestamptz,
    add column if not exists reopen_note text;

alter table public.issues
    drop constraint if exists issues_contractor_status_check,
    add constraint issues_contractor_status_check
        check (contractor_status in (
            'Reported', 'Open for contractors', 'Pending approval', 'Approved',
            'In progress', 'Work done', 'Completed'
        )),
    drop constraint if exists issues_preferred_windows_array_check,
    add constraint issues_preferred_windows_array_check
        check (jsonb_typeof(preferred_windows) = 'array'),
    drop constraint if exists issues_category_length_check,
    add constraint issues_category_length_check
        check (category is null or char_length(category) between 1 and 80),
    drop constraint if exists issues_reopen_note_length_check,
    add constraint issues_reopen_note_length_check
        check (reopen_note is null or char_length(reopen_note) <= 2000);

create index if not exists issues_contractor_queue
    on public.issues(landlord_id, category, created_at desc)
    where released_to_contractors and assigned_contractor_id is null;
create index if not exists issues_contractor_assigned
    on public.issues(assigned_contractor_id, contractor_status);

create or replace function public.is_contractor_user()
returns boolean
language sql stable security definer
set search_path = ''
as $$
    select exists (
        select 1 from public.contractors c where c.auth_user_id = auth.uid()
    );
$$;

create or replace function public.is_active_contractor_for_landlord(
    target_landlord_id uuid,
    target_contractor_id uuid
)
returns boolean
language sql stable security definer
set search_path = ''
as $$
    select exists (
        select 1 from public.landlord_contractors lc
        where lc.landlord_id = target_landlord_id
          and lc.contractor_id = target_contractor_id
          and lc.active
          and (
              lc.landlord_id = auth.uid()
              or exists (
                  select 1 from public.contractors c
                  where c.id = lc.contractor_id and c.auth_user_id = auth.uid()
              )
          )
    );
$$;

create or replace function public.can_access_contractor_unit(
    target_landlord_id uuid,
    target_contractor_id uuid,
    target_unit_id uuid
)
returns boolean
language sql stable security definer
set search_path = ''
as $$
    select exists (
        select 1
        from public.landlord_contractors lc
        where lc.landlord_id = target_landlord_id
          and lc.contractor_id = target_contractor_id
          and lc.active
          and (
              lc.all_units
              or exists (
                  select 1 from public.contractor_unit_access cua
                  where cua.landlord_id = lc.landlord_id
                    and cua.contractor_id = lc.contractor_id
                    and cua.unit_id = target_unit_id
              )
          )
          and (
              target_landlord_id = auth.uid()
              or exists (
                  select 1 from public.contractors c
                  where c.id = target_contractor_id and c.auth_user_id = auth.uid()
              )
          )
    );
$$;

create or replace function public.can_view_contractor(target_contractor_id uuid)
returns boolean
language sql stable security definer
set search_path = ''
as $$
    select exists (
        select 1 from public.contractors c
        where c.id = target_contractor_id and c.auth_user_id = auth.uid()
    ) or exists (
        select 1 from public.landlord_contractors lc
        where lc.contractor_id = target_contractor_id and lc.landlord_id = auth.uid()
    );
$$;

create or replace function public.is_current_contractor(target_contractor_id uuid)
returns boolean
language sql stable security definer
set search_path = ''
as $$
    select exists (
        select 1 from public.contractors c
        where c.id = target_contractor_id and c.auth_user_id = auth.uid()
    );
$$;

create or replace function public.can_read_contractor_media(target_storage_path text)
returns boolean
language sql stable security definer
set search_path = ''
as $$
    select exists (
        select 1
        from public.media m
        join public.issues i on i.id = m.issue_id
        join public.contractors c on c.id = i.assigned_contractor_id
        where m.storage_path = target_storage_path
          and c.auth_user_id = auth.uid()
          and public.is_active_contractor_for_landlord(i.landlord_id, c.id)
    );
$$;

revoke all on function public.is_contractor_user() from public, anon;
revoke all on function public.is_active_contractor_for_landlord(uuid, uuid) from public, anon;
revoke all on function public.can_access_contractor_unit(uuid, uuid, uuid) from public, anon;
revoke all on function public.can_view_contractor(uuid) from public, anon;
revoke all on function public.is_current_contractor(uuid) from public, anon;
revoke all on function public.can_read_contractor_media(text) from public, anon;
grant execute on function public.is_contractor_user() to authenticated;
grant execute on function public.is_active_contractor_for_landlord(uuid, uuid) to authenticated;
grant execute on function public.can_access_contractor_unit(uuid, uuid, uuid) to authenticated;
grant execute on function public.can_view_contractor(uuid) to authenticated;
grant execute on function public.is_current_contractor(uuid) to authenticated;
grant execute on function public.can_read_contractor_media(text) to authenticated;

create or replace function public.record_contractor_reassignment()
returns trigger
language plpgsql security definer
set search_path = ''
as $$
begin
    if new.assigned_contractor_id is distinct from old.assigned_contractor_id then
        if auth.uid() is null then
            raise exception 'An authenticated user is required to reassign contractor work';
        end if;
        insert into public.reassignment_log (
            issue_id, previous_contractor, new_contractor, changed_by
        ) values (
            new.id, old.assigned_contractor_id, new.assigned_contractor_id, auth.uid()
        );
    end if;
    return new;
end;
$$;

drop trigger if exists issues_record_contractor_reassignment on public.issues;
create trigger issues_record_contractor_reassignment
    after update of assigned_contractor_id on public.issues
    for each row execute function public.record_contractor_reassignment();

create or replace function public.protect_contractor_issue_fields()
returns trigger
language plpgsql security definer
set search_path = ''
as $$
begin
    if tg_op = 'INSERT' then
        new.contractor_status := 'Reported';
        new.released_to_contractors := false;
        new.assigned_contractor_id := null;
        new.accepted_at := null;
        new.landlord_seen_at := null;
        new.work_done_at := null;
        new.tenant_verified_at := null;
        new.reopen_note := null;
        return new;
    end if;

    if new.category is not distinct from old.category
        and new.preferred_windows is not distinct from old.preferred_windows
        and new.released_to_contractors is not distinct from old.released_to_contractors
        and new.assigned_contractor_id is not distinct from old.assigned_contractor_id
        and new.contractor_status is not distinct from old.contractor_status
        and new.accepted_at is not distinct from old.accepted_at
        and new.landlord_seen_at is not distinct from old.landlord_seen_at
        and new.work_done_at is not distinct from old.work_done_at
        and new.tenant_verified_at is not distinct from old.tenant_verified_at
        and new.reopen_note is not distinct from old.reopen_note then
        return new;
    end if;

    if auth.uid() = old.landlord_id and public.is_landlord_user() then
        return new;
    end if;

    if exists (
        select 1 from public.contractors c
        where c.auth_user_id = auth.uid()
          and (
              c.id = old.assigned_contractor_id
              or c.id = new.assigned_contractor_id
          )
    ) then
        return new;
    end if;

    if auth.uid() = old.tenant_id
        and public.is_tenant_user()
        and new.tenant_verified_at is distinct from old.tenant_verified_at
        and old.contractor_status = 'Work done'
        and new.contractor_status = 'Completed'
        and new.tenant_verified_at is not null
        and new.category is not distinct from old.category
        and new.preferred_windows is not distinct from old.preferred_windows
        and new.released_to_contractors is not distinct from old.released_to_contractors
        and new.assigned_contractor_id is not distinct from old.assigned_contractor_id
        and new.accepted_at is not distinct from old.accepted_at
        and new.landlord_seen_at is not distinct from old.landlord_seen_at
        and new.work_done_at is not distinct from old.work_done_at
        and new.reopen_note is not distinct from old.reopen_note then
        return new;
    end if;

    if auth.uid() = old.tenant_id
        and public.is_tenant_user()
        and old.contractor_status = 'Work done'
        and old.tenant_verified_at is null
        and new.contractor_status = 'In progress'
        and new.tenant_verified_at is not distinct from old.tenant_verified_at
        and new.category is not distinct from old.category
        and new.preferred_windows is not distinct from old.preferred_windows
        and new.released_to_contractors is not distinct from old.released_to_contractors
        and new.assigned_contractor_id is not distinct from old.assigned_contractor_id
        and new.accepted_at is not distinct from old.accepted_at
        and new.landlord_seen_at is not distinct from old.landlord_seen_at
        and new.work_done_at is null
        and new.reopen_note is distinct from old.reopen_note
        and new.reopen_note is not null then
        return new;
    end if;

    if auth.uid() is null
        and old.contractor_status = 'Work done'
        and old.tenant_verified_at is null
        and old.work_done_at <= now() - interval '7 days'
        and new.contractor_status = 'Completed'
        and new.category is not distinct from old.category
        and new.preferred_windows is not distinct from old.preferred_windows
        and new.released_to_contractors is not distinct from old.released_to_contractors
        and new.assigned_contractor_id is not distinct from old.assigned_contractor_id
        and new.accepted_at is not distinct from old.accepted_at
        and new.landlord_seen_at is not distinct from old.landlord_seen_at
        and new.work_done_at is not distinct from old.work_done_at
        and new.tenant_verified_at is not distinct from old.tenant_verified_at
        and new.reopen_note is not distinct from old.reopen_note then
        return new;
    end if;

    raise exception 'Contractor issue fields can only be changed by the landlord or approved workflow';
end;
$$;

drop trigger if exists issues_protect_contractor_fields on public.issues;
create trigger issues_protect_contractor_fields
    before insert or update on public.issues
    for each row execute function public.protect_contractor_issue_fields();

create or replace view public.contractor_job_feed
with (security_barrier = true)
as
select
    i.id as issue_id,
    i.title,
    i.description,
    i.category,
    case when i.contractor_status in ('Approved', 'In progress', 'Work done', 'Completed')
        then i.preferred_windows else '[]'::jsonb end as preferred_windows,
    i.severity,
    i.contractor_status,
    i.created_at,
    i.accepted_at,
    i.landlord_seen_at,
    i.work_done_at,
    i.tenant_verified_at,
    case when i.contractor_status in ('Approved', 'In progress', 'Work done', 'Completed')
        then p.address else null end as property_address,
    case when i.contractor_status in ('Approved', 'In progress', 'Work done', 'Completed')
        then u.unit_number else null end as unit_number,
    coalesce(i.assigned_contractor_id = c.id, false) as assigned_to_me,
    i.landlord_id,
    coalesce(nullif(landlord.full_name, ''), p.name) as landlord_label,
    p.name as building_label,
    case when position(',' in p.address) > 0
        then trim(split_part(p.address, ',', array_length(string_to_array(p.address, ','), 1)))
        else null end as suburb,
    i.reopen_note,
    i.assigned_contractor_id
from public.issues i
join public.units u on u.id = i.unit_id
join public.properties p on p.id = i.property_id
join public.users landlord on landlord.id = i.landlord_id
join public.contractors c on c.auth_user_id = auth.uid()
where
    (
        i.released_to_contractors
        and i.assigned_contractor_id is null
        and i.contractor_status = 'Open for contractors'
        and public.can_access_contractor_unit(i.landlord_id, c.id, i.unit_id)
        and exists (
            select 1 from public.contractor_trades t
            where t.contractor_id = c.id and t.category = i.category
        )
    )
    or (
        i.assigned_contractor_id = c.id
        and public.is_active_contractor_for_landlord(i.landlord_id, c.id)
    );

grant select on public.contractor_job_feed to authenticated;

alter table public.issue_events
    drop constraint if exists issue_events_author_role_check,
    add constraint issue_events_author_role_check
        check (author_role in ('Tenant', 'Landlord', 'Contractor'));

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
    author_name text;
    author_role text;
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
    if object_size is null or object_mime is distinct from new.mime_type
        or new.file_size_bytes <> object_size then
        raise exception 'Stored media metadata does not match the uploaded object';
    end if;
    if new.mime_type in ('image/jpeg', 'image/png', 'image/heic') then
        if object_size > 10485760 or new.duration_seconds is not null then
            raise exception 'Photos must not exceed 10 MB and cannot have a video duration';
        end if;
    elsif new.mime_type in ('video/mp4', 'video/quicktime') then
        if object_size > 52428800 or new.duration_seconds is null or new.duration_seconds > 30 then
            raise exception 'Videos must not exceed 50 MB or 30 seconds';
        end if;
    else
        raise exception 'Unsupported media type';
    end if;

    select
        case when u.role = 'contractor' then c.display_name else u.full_name end,
        case u.role
            when 'tenant' then 'Tenant'
            when 'landlord' then 'Landlord'
            when 'contractor' then 'Contractor'
            else null
        end
    into author_name, author_role
    from public.users u
    left join public.contractors c on c.auth_user_id = u.id
    where u.id = new.uploaded_by;
    if author_name is null or author_role is null then
        raise exception 'The media uploader has no supported Rent Log role';
    end if;

    new.file_size_bytes := object_size;
    insert into public.issue_events (
        issue_id, author_id, author_name, author_role, event_type, description
    )
    values (
        new.issue_id, new.uploaded_by, author_name, author_role,
        'MediaUpload', 'Evidence attached: ' || new.file_name
    )
    returning id into new.event_id;
    return new;
end;
$$;

create or replace function public.add_contractor_job_update(
    p_issue_id uuid,
    p_note text,
    p_media jsonb
)
returns void
language plpgsql security definer
set search_path = ''
as $$
declare
    contractor public.contractors%rowtype;
    issue_event_id uuid;
    item jsonb;
    current_photos integer;
    current_videos integer;
    added_photos integer;
    added_videos integer;
begin
    select c.* into contractor
    from public.contractors c
    where c.auth_user_id = auth.uid();

    if contractor.id is null then
        raise exception 'A contractor account is required';
    end if;
    if p_note is null or length(trim(p_note)) > 2000
        or p_media is null or jsonb_typeof(p_media) is distinct from 'array'
        or (length(trim(p_note)) = 0 and jsonb_array_length(p_media) = 0) then
        raise exception 'Add a note or at least one photo or video';
    end if;

    if not exists (
        select 1 from public.issues i
        where i.id = p_issue_id
          and i.assigned_contractor_id = contractor.id
          and i.contractor_status = 'In progress'
          and public.is_active_contractor_for_landlord(i.landlord_id, contractor.id)
    ) then
        raise exception 'Only the active contractor on an in-progress job can add updates';
    end if;

    select count(*) filter (where m.mime_type like 'image/%')::integer,
           count(*) filter (where m.mime_type like 'video/%')::integer
    into current_photos, current_videos
    from public.media m
    where m.issue_id = p_issue_id;

    select count(*) filter (where media_item.value ->> 'mime_type' like 'image/%')::integer,
           count(*) filter (where media_item.value ->> 'mime_type' like 'video/%')::integer
    into added_photos, added_videos
    from jsonb_array_elements(p_media) as media_item(value);

    if current_photos + added_photos > 5 or current_videos + added_videos > 2 then
        raise exception 'This issue has reached its photo or video limit';
    end if;

    insert into public.issue_events (
        issue_id, author_id, author_name, author_role, event_type, description
    ) values (
        p_issue_id,
        auth.uid(),
        contractor.display_name,
        'Contractor',
        case when length(trim(p_note)) > 0 then 'Comment' else 'MediaUpload' end,
        coalesce(nullif(trim(p_note), ''), 'Contractor uploaded job evidence.')
    )
    returning id into issue_event_id;

    for item in select media_item.value from jsonb_array_elements(p_media) as media_item(value)
    loop
        insert into public.media (
            issue_id, event_id, uploaded_by, storage_path, file_name,
            mime_type, file_size_bytes, alt_text, duration_seconds
        ) values (
            p_issue_id,
            issue_event_id,
            auth.uid(),
            item ->> 'storage_path',
            item ->> 'file_name',
            item ->> 'mime_type',
            (item ->> 'file_size_bytes')::bigint,
            coalesce(item ->> 'alt_text', ''),
            nullif(item ->> 'duration_seconds', '')::numeric
        );
    end loop;
end;
$$;

create or replace function public.update_contractor_profile(
    p_service_area text,
    p_trades text[]
)
returns void
language plpgsql security definer
set search_path = ''
as $$
declare
    v_contractor_id uuid;
begin
    select c.id into v_contractor_id
    from public.contractors c
    where c.auth_user_id = auth.uid();
    if v_contractor_id is null then
        raise exception 'A contractor account is required';
    end if;
    if p_service_area is null or length(trim(p_service_area)) > 200
        or p_trades is null or cardinality(p_trades) = 0
        or exists (
            select 1 from unnest(p_trades) as trade(category)
            where trade.category is null or length(trim(trade.category)) not between 1 and 80
        )
        or not exists (
            select 1 from unnest(p_trades) as trade(category)
            where length(trim(trade.category)) between 1 and 80
        ) then
        raise exception 'A service area and at least one valid trade are required';
    end if;

    update public.contractors
    set service_area = trim(p_service_area)
    where id = v_contractor_id;
    delete from public.contractor_trades ct where ct.contractor_id = v_contractor_id;
    insert into public.contractor_trades (contractor_id, category)
    select v_contractor_id, trim(trade.category)
    from unnest(p_trades) as trade(category)
    where length(trim(trade.category)) > 0
    on conflict do nothing;
end;
$$;

create or replace view public.contractor_media_feed
with (security_barrier = true)
as
select m.issue_id, m.storage_path, m.file_name, m.mime_type, m.created_at
from public.media m
join public.issues i on i.id = m.issue_id
join public.contractors c on c.id = i.assigned_contractor_id
where c.auth_user_id = auth.uid()
  and public.is_active_contractor_for_landlord(i.landlord_id, c.id);

grant select on public.contractor_media_feed to authenticated;

create or replace function public.accept_contractor_job(p_issue_id uuid)
returns public.contractor_job_feed
language plpgsql security definer
set search_path = ''
as $$
declare
    contractor uuid;
    claimed_issue uuid;
    claimed_job public.contractor_job_feed%rowtype;
begin
    select c.id into contractor
    from public.contractors c
    where c.auth_user_id = auth.uid();
    if contractor is null then
        raise exception 'A contractor account is required';
    end if;

    update public.issues i
    set assigned_contractor_id = contractor,
        contractor_status = 'Pending approval',
        accepted_at = now()
    where i.id = p_issue_id
      and i.released_to_contractors
      and i.assigned_contractor_id is null
      and i.contractor_status = 'Open for contractors'
      and public.can_access_contractor_unit(i.landlord_id, contractor, i.unit_id)
      and exists (
          select 1 from public.contractor_trades t
          where t.contractor_id = contractor and t.category = i.category
      )
    returning i.id into claimed_issue;

    if claimed_issue is null then
        return null;
    end if;
    select * into claimed_job
    from public.contractor_job_feed f where f.issue_id = claimed_issue;
    return claimed_job;
end;
$$;

create or replace function public.release_contractor_job(p_issue_id uuid, p_release boolean)
returns void
language plpgsql security definer
set search_path = ''
as $$
begin
    update public.issues i
    set released_to_contractors = p_release,
        contractor_status = case when p_release then 'Open for contractors' else 'Reported' end
    where i.id = p_issue_id
      and i.landlord_id = auth.uid()
      and public.is_landlord_user()
      and i.assigned_contractor_id is null
      and (
          (p_release and i.category is not null and length(trim(i.category)) > 0
              and i.contractor_status = 'Reported')
          or (not p_release and i.contractor_status = 'Open for contractors')
      );
    if not found then
        raise exception 'The job is not eligible for this release change';
    end if;
end;
$$;

create or replace function public.set_contractor_issue_details(
    p_issue_id uuid,
    p_category text,
    p_preferred_windows jsonb
)
returns void
language plpgsql security definer
set search_path = ''
as $$
begin
    if p_category is null or length(trim(p_category)) not between 1 and 80
        or p_preferred_windows is null
        or jsonb_typeof(p_preferred_windows) is distinct from 'array' then
        raise exception 'A category and a list of preferred work windows are required';
    end if;
    update public.issues i
    set category = trim(p_category),
        preferred_windows = p_preferred_windows
    where i.id = p_issue_id
      and i.landlord_id = auth.uid()
      and public.is_landlord_user()
      and i.assigned_contractor_id is null
      and i.contractor_status in ('Reported', 'Open for contractors');
    if not found then
        raise exception 'The job details cannot be changed after assignment';
    end if;
end;
$$;

create or replace function public.approve_contractor_job(p_issue_id uuid)
returns void
language plpgsql security definer
set search_path = ''
as $$
begin
    update public.issues i
    set contractor_status = 'Approved'
    where i.id = p_issue_id
      and i.landlord_id = auth.uid()
      and public.is_landlord_user()
      and i.assigned_contractor_id is not null
      and i.contractor_status = 'Pending approval'
      and public.is_active_contractor_for_landlord(i.landlord_id, i.assigned_contractor_id);
    if not found then
        raise exception 'The job is not awaiting approval';
    end if;
end;
$$;

create or replace function public.mark_contractor_job_seen(p_issue_id uuid)
returns void
language plpgsql security definer
set search_path = ''
as $$
begin
    update public.issues
    set landlord_seen_at = now()
    where id = p_issue_id and landlord_id = auth.uid() and public.is_landlord_user();
    if not found then
        raise exception 'The job was not found for this landlord';
    end if;
end;
$$;

create or replace function public.withdraw_contractor_job(p_issue_id uuid)
returns void
language plpgsql security definer
set search_path = ''
as $$
begin
    update public.issues i
    set assigned_contractor_id = null,
        contractor_status = 'Open for contractors',
        accepted_at = null,
        released_to_contractors = true
    where i.id = p_issue_id
      and i.assigned_contractor_id = (
          select c.id from public.contractors c where c.auth_user_id = auth.uid()
      )
      and public.is_active_contractor_for_landlord(
          i.landlord_id,
          i.assigned_contractor_id
      )
      and i.released_to_contractors
      and i.contractor_status in ('Pending approval', 'Approved');
    if not found then
        raise exception 'The job cannot be withdrawn after work has started';
    end if;

    insert into public.landlord_notifications (landlord_id, issue_id, message)
    select i.landlord_id, i.id, 'The contractor withdrew from this job. It is open for contractors again.'
    from public.issues i
    where i.id = p_issue_id;
end;
$$;

create or replace function public.reassign_contractor_job(
    p_issue_id uuid,
    p_contractor_id uuid
)
returns void
language plpgsql security definer
set search_path = ''
as $$
begin
    update public.issues i
    set assigned_contractor_id = p_contractor_id,
        contractor_status = 'Pending approval',
        accepted_at = now(),
        released_to_contractors = true
    where i.id = p_issue_id
      and i.landlord_id = auth.uid()
      and public.is_landlord_user()
      and i.contractor_status not in ('In progress', 'Work done', 'Completed')
      and public.can_access_contractor_unit(i.landlord_id, p_contractor_id, i.unit_id)
      and exists (
          select 1 from public.contractor_trades t
          where t.contractor_id = p_contractor_id and t.category = i.category
      );
    if not found then
        raise exception 'The job cannot be reassigned to this contractor';
    end if;
end;
$$;

create or replace function public.return_contractor_job_to_board(p_issue_id uuid)
returns void
language plpgsql security definer
set search_path = ''
as $$
begin
    update public.issues i
    set assigned_contractor_id = null,
        contractor_status = 'Open for contractors',
        accepted_at = null,
        released_to_contractors = true
    where i.id = p_issue_id
      and i.landlord_id = auth.uid()
      and public.is_landlord_user()
      and i.assigned_contractor_id is not null
      and i.contractor_status = 'Pending approval';
    if not found then
        raise exception 'The job is not awaiting approval';
    end if;
end;
$$;

create or replace function public.set_contractor_work_status(
    p_issue_id uuid,
    p_status text
)
returns void
language plpgsql security definer
set search_path = ''
as $$
declare
    contractor uuid;
begin
    select c.id into contractor from public.contractors c where c.auth_user_id = auth.uid();
    if p_status not in ('In progress', 'Work done') then
        raise exception 'Unsupported contractor work status';
    end if;

    update public.issues i
    set contractor_status = p_status,
        work_done_at = case when p_status = 'Work done' then now() else null end,
        tenant_verified_at = null
    where i.id = p_issue_id
      and i.assigned_contractor_id = contractor
      and (
          (p_status = 'In progress' and i.contractor_status = 'Approved')
          or (p_status = 'Work done' and i.contractor_status = 'In progress')
      )
      and public.is_active_contractor_for_landlord(i.landlord_id, contractor);
    if not found then
        raise exception 'The contractor cannot update this job';
    end if;

    insert into public.issue_events (
        issue_id, author_id, author_name, author_role, event_type, description
    )
    select i.id, auth.uid(), c.display_name, 'Contractor', 'StatusChange',
        case when p_status = 'Work done'
            then 'The contractor marked the work done.'
            else 'The contractor started work.' end
    from public.issues i
    join public.contractors c on c.id = contractor
    where i.id = p_issue_id;

    if p_status = 'Work done' then
        insert into public.landlord_notifications (landlord_id, issue_id, message)
        select i.landlord_id, i.id, 'The contractor marked the work done. Please review the tenant issue.'
        from public.issues i
        where i.id = p_issue_id;
    end if;
end;
$$;

create or replace function public.verify_contractor_work(p_issue_id uuid)
returns void
language plpgsql security definer
set search_path = ''
as $$
begin
    update public.issues i
    set contractor_status = 'Completed',
        tenant_verified_at = now()
    where i.id = p_issue_id
      and i.tenant_id = auth.uid()
      and public.is_tenant_user()
      and i.contractor_status = 'Work done'
      and i.tenant_verified_at is null;
    if not found then
        raise exception 'The job is not awaiting tenant verification';
    end if;
end;
$$;

create or replace function public.get_tenant_issue_contractor_details(p_issue_id uuid)
returns table(contractor_first_name text, contractor_trade text)
language sql
stable
security definer
set search_path = ''
as $$
    select
        split_part(trim(c.display_name), ' ', 1),
        coalesce(t.category, '')
    from public.issues i
    join public.contractors c on c.id = i.assigned_contractor_id
    left join public.contractor_trades t
        on t.contractor_id = c.id
       and t.category = i.category
    where i.id = p_issue_id
      and i.tenant_id = auth.uid()
      and public.is_tenant_user()
    order by t.category nulls last
    limit 1;
$$;

create or replace function public.reopen_contractor_work(p_issue_id uuid, p_note text)
returns void
language plpgsql security definer
set search_path = ''
as $$
begin
    if p_note is null or length(trim(p_note)) not between 1 and 500 then
        raise exception 'A reopening note between 1 and 500 characters is required';
    end if;
    update public.issues i
    set contractor_status = 'In progress',
        work_done_at = null,
        tenant_verified_at = null,
        reopen_note = trim(p_note)
    where i.id = p_issue_id
      and i.tenant_id = auth.uid()
      and public.is_tenant_user()
      and i.contractor_status = 'Work done'
      and i.tenant_verified_at is null;
    if not found then
        raise exception 'The job cannot be reopened';
    end if;

    insert into public.landlord_notifications (landlord_id, issue_id, message)
    select i.landlord_id, i.id, 'The tenant reopened the contractor work.'
    from public.issues i
    where i.id = p_issue_id;
end;
$$;

create or replace function public.remove_landlord_contractor(p_contractor_id uuid)
returns void
language plpgsql security definer
set search_path = ''
as $$
declare
    blocked_jobs text;
begin
    if not public.is_landlord_user() then
        raise exception 'Only a landlord can remove a contractor';
    end if;
    if not exists (
        select 1 from public.landlord_contractors
        where landlord_id = auth.uid() and contractor_id = p_contractor_id
    ) then
        raise exception 'The contractor is not linked to this landlord';
    end if;

    select string_agg(
        format('%s (unit %s)', i.title, i.unit_number),
        ', ' order by i.created_at
    ) into blocked_jobs
    from public.issues i
    where i.landlord_id = auth.uid()
      and i.assigned_contractor_id = p_contractor_id
      and i.contractor_status in ('Approved', 'In progress');
    if blocked_jobs is not null then
        raise exception 'Reassign these jobs before removing this contractor: %', blocked_jobs;
    end if;

    update public.issues i
    set assigned_contractor_id = null,
        contractor_status = case
            when i.contractor_status = 'Pending approval' then 'Open for contractors'
            else i.contractor_status
        end,
        accepted_at = case
            when i.contractor_status = 'Pending approval' then null
            else i.accepted_at
        end,
        released_to_contractors = true
    where i.landlord_id = auth.uid()
      and i.assigned_contractor_id = p_contractor_id
      and i.contractor_status not in ('Approved', 'In progress');

    delete from public.contractor_unit_access
    where landlord_id = auth.uid() and contractor_id = p_contractor_id;

    update public.landlord_contractors
    set active = false
    where landlord_id = auth.uid() and contractor_id = p_contractor_id;
end;
$$;

create or replace function public.set_landlord_contractor_active(
    p_contractor_id uuid,
    p_active boolean
)
returns void
language plpgsql security definer
set search_path = ''
as $$
begin
    if not public.is_landlord_user() then
        raise exception 'Only a landlord can update a contractor link';
    end if;

    if p_active then
        update public.landlord_contractors
        set active = true
        where landlord_id = auth.uid()
          and contractor_id = p_contractor_id;
        if not found then
            raise exception 'The contractor is not linked to this landlord';
        end if;
    else
        perform public.remove_landlord_contractor(p_contractor_id);
    end if;
end;
$$;

create or replace function public.redeem_contractor_invite(
    p_token text,
    p_display_name text,
    p_contact_details jsonb,
    p_service_area text,
    p_trades text[]
)
returns uuid
language plpgsql security definer
set search_path = ''
as $$
declare
    invited_landlord uuid;
    contractor uuid;
begin
    update public.contractor_invites
    set used = true
    where token = p_token and not used and expires_at > now()
    returning landlord_id into invited_landlord;
    if invited_landlord is null then
        raise exception 'Contractor invite is invalid, expired, or already used';
    end if;
    if auth.uid() is null or not exists (
        select 1 from public.users u where u.id = auth.uid()
    ) or public.is_landlord_user() then
        raise exception 'Sign in before redeeming a contractor invite';
    end if;
    if p_display_name is null
        or length(trim(p_display_name)) not between 1 and 120
        or p_contact_details is null
        or jsonb_typeof(p_contact_details) is distinct from 'object'
        or p_trades is null or cardinality(p_trades) = 0 then
        raise exception 'A display name, contact details, and at least one trade are required';
    end if;

    insert into public.contractors (auth_user_id, display_name, contact_details, service_area)
    values (auth.uid(), trim(p_display_name), p_contact_details, trim(p_service_area))
    on conflict (auth_user_id) do update
        set display_name = excluded.display_name,
            contact_details = excluded.contact_details,
            service_area = excluded.service_area
    returning id into contractor;

    insert into public.contractor_trades (contractor_id, category)
    select contractor, trim(category)
    from unnest(p_trades) as trade(category)
    where length(trim(category)) between 1 and 80
    on conflict do nothing;

    if not exists (select 1 from public.contractor_trades t where t.contractor_id = contractor) then
        raise exception 'At least one valid trade is required';
    end if;

    insert into public.landlord_contractors (landlord_id, contractor_id, active)
    values (invited_landlord, contractor, true)
    on conflict (landlord_id, contractor) do update
        set active = true, all_units = true;
    return contractor;
end;
$$;

create or replace function public.redeem_unit_invite(p_token text)
returns void
language plpgsql security definer
set search_path = ''
as $$
declare
    invited_unit uuid;
    invited_landlord uuid;
    contractor uuid;
    invited_tenant uuid := auth.uid();
begin
    update public.unit_invites ui
    set used = true
    where ui.token = p_token and not ui.used and ui.expires_at > now()
    returning ui.unit_id into invited_unit;
    if invited_unit is null then
        raise exception 'Unit invite is invalid, expired, or already used';
    end if;
    select p.landlord_id into invited_landlord
    from public.units u join public.properties p on p.id = u.property_id
    where u.id = invited_unit;

    if public.is_tenant_user() then
        insert into public.tenancies (unit_id, tenant_id, start_date, is_active)
        values (invited_unit, invited_tenant, current_date, true)
        on conflict (unit_id, tenant_id) where is_active do nothing;
        return;
    end if;

    select c.id into contractor from public.contractors c where c.auth_user_id = auth.uid();
    if contractor is null or public.is_landlord_user() then
        raise exception 'A tenant or contractor account is required';
    end if;

    insert into public.landlord_contractors (landlord_id, contractor_id, active, all_units)
    values (invited_landlord, contractor, true, false)
    on conflict (landlord_id, contractor) do update
        set active = true,
            all_units = case
                when public.landlord_contractors.active then public.landlord_contractors.all_units
                else false
            end;
    insert into public.contractor_unit_access (landlord_id, contractor_id, unit_id)
    values (invited_landlord, contractor, invited_unit)
    on conflict (contractor_id, unit_id) do nothing;
end;
$$;

create or replace function public.preview_unit_invite(p_token text)
returns jsonb
language sql
stable
security definer
set search_path = ''
as $$
    select coalesce(
        (
            select jsonb_build_object(
                'status', case
                    when ui.used then 'used'
                    when ui.expires_at <= now() then 'expired'
                    else 'valid'
                end,
                'building_name', p.name,
                'unit_name', u.unit_number
            )
            from public.unit_invites ui
            join public.units u on u.id = ui.unit_id
            join public.properties p on p.id = u.property_id
            where ui.token = p_token
        ),
        jsonb_build_object('status', 'invalid', 'building_name', '', 'unit_name', '')
    );
$$;

create or replace function public.preview_contractor_invite(p_token text)
returns jsonb
language sql
stable
security definer
set search_path = ''
as $$
    select coalesce(
        (
            select jsonb_build_object(
                'status', case
                    when ci.used then 'used'
                    when ci.expires_at <= now() then 'expired'
                    else 'valid'
                end,
                'landlord_name', u.full_name
            )
            from public.contractor_invites ci
            join public.users u on u.id = ci.landlord_id
            where ci.token = p_token
        ),
        jsonb_build_object('status', 'invalid', 'landlord_name', '')
    );
$$;

create or replace function public.link_contractor_invite(p_token text)
returns void
language plpgsql
security definer
set search_path = ''
as $$
declare
    invited_landlord uuid;
    contractor uuid;
begin
    if public.is_landlord_user() then
        raise exception 'A contractor account is required';
    end if;
    select c.id into contractor
    from public.contractors c
    where c.auth_user_id = auth.uid();
    if contractor is null then
        raise exception 'This account is not registered as a contractor';
    end if;

    update public.contractor_invites
    set used = true
    where token = p_token and not used and expires_at > now()
    returning landlord_id into invited_landlord;
    if invited_landlord is null then
        raise exception 'Contractor invite is invalid, expired, or already used';
    end if;

    insert into public.landlord_contractors (landlord_id, contractor_id, active, all_units)
    values (invited_landlord, contractor, true, true)
    on conflict (landlord_id, contractor_id) do update
        set active = true, all_units = true;
end;
$$;

create or replace function public.create_rent_log_profile()
returns trigger
language plpgsql
security definer
set search_path = ''
as $$
declare
    tenant_invite_unit uuid;
    contractor_invite_landlord uuid;
    new_contractor_id uuid;
    trades_payload jsonb;
begin
    if nullif(new.raw_user_meta_data ->> 'unit_invite_token', '') is not null
        and nullif(new.raw_user_meta_data ->> 'contractor_invite_token', '') is not null then
        raise exception 'Only one invitation may be used for account registration';
    end if;

    if nullif(new.raw_user_meta_data ->> 'unit_invite_token', '') is not null then
        update public.unit_invites
        set used = true
        where token = new.raw_user_meta_data ->> 'unit_invite_token'
          and not used
          and expires_at > now()
        returning unit_id into tenant_invite_unit;
        if tenant_invite_unit is null then
            raise exception 'Unit invite is invalid, expired, or already used';
        end if;
    end if;

    if nullif(new.raw_user_meta_data ->> 'contractor_invite_token', '') is not null then
        update public.contractor_invites
        set used = true
        where token = new.raw_user_meta_data ->> 'contractor_invite_token'
          and not used
          and expires_at > now()
        returning landlord_id into contractor_invite_landlord;
        if contractor_invite_landlord is null then
            raise exception 'Contractor invite is invalid, expired, or already used';
        end if;

        trades_payload := new.raw_user_meta_data -> 'contractor_trades';
        if jsonb_typeof(trades_payload) is distinct from 'array' then
            raise exception 'Contractor trades must be provided as an array';
        end if;
        if jsonb_array_length(trades_payload) = 0 then
            raise exception 'At least one contractor trade is required';
        end if;
        if length(trim(coalesce(new.raw_user_meta_data ->> 'full_name', ''))) not between 1 and 120 then
            raise exception 'A contractor name between 1 and 120 characters is required';
        end if;
    end if;

    insert into public.users (id, email, full_name, role)
    values (
        new.id,
        coalesce(new.email, ''),
        coalesce(nullif(new.raw_user_meta_data ->> 'full_name', ''), split_part(coalesce(new.email, 'Tenant'), '@', 1)),
        case
            when contractor_invite_landlord is not null then 'contractor'
            else 'tenant'
        end
    )
    on conflict (id) do update set email = excluded.email;

    if tenant_invite_unit is not null then
        insert into public.tenancies (unit_id, tenant_id, start_date, is_active)
        values (tenant_invite_unit, new.id, current_date, true);
    end if;

    if contractor_invite_landlord is not null then
        insert into public.contractors (auth_user_id, display_name, service_area)
        values (
            new.id,
            trim(new.raw_user_meta_data ->> 'full_name'),
            trim(coalesce(new.raw_user_meta_data ->> 'service_area', ''))
        )
        returning id into new_contractor_id;

        insert into public.contractor_trades (contractor_id, category)
        select new_contractor_id, trim(trade.value)
        from jsonb_array_elements_text(trades_payload) as trade(value)
        where length(trim(trade.value)) between 1 and 80
        on conflict do nothing;

        if not exists (
            select 1 from public.contractor_trades t where t.contractor_id = new_contractor_id
        ) then
            raise exception 'At least one valid contractor trade is required';
        end if;

        insert into public.landlord_contractors (landlord_id, contractor_id, active, all_units)
        values (contractor_invite_landlord, new_contractor_id, true, true);
    end if;
    return new;
end;
$$;

alter table public.users drop constraint if exists users_role_check;
alter table public.users
    add constraint users_role_check check (role in ('tenant', 'landlord', 'contractor'));

revoke all on function public.create_rent_log_profile() from public, anon, authenticated;
revoke all on function public.preview_unit_invite(text) from public;
revoke all on function public.preview_contractor_invite(text) from public;
revoke all on function public.link_contractor_invite(text) from public, anon;
grant execute on function public.preview_unit_invite(text) to anon, authenticated;
grant execute on function public.preview_contractor_invite(text) to anon, authenticated;
grant execute on function public.link_contractor_invite(text) to authenticated;

create or replace function public.complete_stale_contractor_work()
returns integer
language plpgsql security definer
set search_path = ''
as $$
declare
    completed_count integer;
begin
    with completed as (
        update public.issues i
        set contractor_status = 'Completed'
        where i.contractor_status = 'Work done'
          and i.tenant_verified_at is null
          and i.work_done_at <= now() - interval '7 days'
        returning i.id, i.landlord_id
    )
    insert into public.landlord_notifications (landlord_id, issue_id, message)
    select c.landlord_id, c.id,
        'Contractor work was automatically completed after 7 days without tenant verification.'
    from completed c;

    get diagnostics completed_count = row_count;
    return completed_count;
end;
$$;

create or replace function public.mark_landlord_notification_read(p_notification_id uuid)
returns void
language plpgsql security definer
set search_path = ''
as $$
begin
    update public.landlord_notifications
    set read_at = now()
    where id = p_notification_id
      and landlord_id = auth.uid()
      and public.is_landlord_user();
    if not found then
        raise exception 'The notification was not found for this landlord';
    end if;
end;
$$;

revoke all on function public.accept_contractor_job(uuid) from public, anon;
revoke all on function public.release_contractor_job(uuid, boolean) from public, anon;
revoke all on function public.set_contractor_issue_details(uuid, text, jsonb) from public, anon;
revoke all on function public.approve_contractor_job(uuid) from public, anon;
revoke all on function public.mark_contractor_job_seen(uuid) from public, anon;
revoke all on function public.withdraw_contractor_job(uuid) from public, anon;
revoke all on function public.reassign_contractor_job(uuid, uuid) from public, anon;
revoke all on function public.return_contractor_job_to_board(uuid) from public, anon;
revoke all on function public.set_contractor_work_status(uuid, text) from public, anon;
revoke all on function public.add_contractor_job_update(uuid, text, jsonb) from public, anon;
revoke all on function public.update_contractor_profile(text, text[]) from public, anon;
revoke all on function public.verify_contractor_work(uuid) from public, anon;
revoke all on function public.get_tenant_issue_contractor_details(uuid) from public, anon;
revoke all on function public.reopen_contractor_work(uuid, text) from public, anon;
revoke all on function public.remove_landlord_contractor(uuid) from public, anon;
revoke all on function public.set_landlord_contractor_active(uuid, boolean) from public, anon;
revoke all on function public.redeem_contractor_invite(text, text, jsonb, text, text[]) from public, anon;
revoke all on function public.redeem_unit_invite(text) from public, anon;
revoke all on function public.complete_stale_contractor_work() from public, anon, authenticated;
revoke all on function public.mark_landlord_notification_read(uuid) from public, anon;
revoke all on function public.end_tenant_unit_tenancy(uuid, uuid) from public, anon;
grant execute on function public.accept_contractor_job(uuid) to authenticated;
grant execute on function public.release_contractor_job(uuid, boolean) to authenticated;
grant execute on function public.set_contractor_issue_details(uuid, text, jsonb) to authenticated;
grant execute on function public.approve_contractor_job(uuid) to authenticated;
grant execute on function public.mark_contractor_job_seen(uuid) to authenticated;
grant execute on function public.withdraw_contractor_job(uuid) to authenticated;
grant execute on function public.reassign_contractor_job(uuid, uuid) to authenticated;
grant execute on function public.return_contractor_job_to_board(uuid) to authenticated;
grant execute on function public.set_contractor_work_status(uuid, text) to authenticated;
grant execute on function public.add_contractor_job_update(uuid, text, jsonb) to authenticated;
grant execute on function public.update_contractor_profile(text, text[]) to authenticated;
grant execute on function public.verify_contractor_work(uuid) to authenticated;
grant execute on function public.get_tenant_issue_contractor_details(uuid) to authenticated;
grant execute on function public.reopen_contractor_work(uuid, text) to authenticated;
grant execute on function public.remove_landlord_contractor(uuid) to authenticated;
grant execute on function public.set_landlord_contractor_active(uuid, boolean) to authenticated;
grant execute on function public.redeem_contractor_invite(text, text, jsonb, text, text[]) to authenticated;
grant execute on function public.redeem_unit_invite(text) to authenticated;
grant execute on function public.mark_landlord_notification_read(uuid) to authenticated;
grant execute on function public.end_tenant_unit_tenancy(uuid, uuid) to authenticated;

alter table public.contractors enable row level security;
alter table public.contractor_trades enable row level security;
alter table public.landlord_contractors enable row level security;
alter table public.contractor_unit_access enable row level security;
alter table public.contractor_invites enable row level security;
alter table public.unit_invites enable row level security;
alter table public.reassignment_log enable row level security;
alter table public.landlord_notifications enable row level security;

drop policy if exists users_hide_other_profiles_from_contractors on public.users;
create policy users_hide_other_profiles_from_contractors on public.users
    as restrictive for select to authenticated
    using (not public.is_contractor_user() or id = auth.uid());

drop policy if exists contractors_select_related on public.contractors;
create policy contractors_select_related on public.contractors
    for select to authenticated
    using (public.can_view_contractor(id));
drop policy if exists contractors_update_self on public.contractors;
create policy contractors_update_self on public.contractors
    for update to authenticated
    using (auth_user_id = auth.uid())
    with check (auth_user_id = auth.uid());

drop policy if exists contractor_trades_select_related on public.contractor_trades;
create policy contractor_trades_select_related on public.contractor_trades
    for select to authenticated
    using (public.can_view_contractor(contractor_id));
drop policy if exists contractor_trades_write_self on public.contractor_trades;
create policy contractor_trades_write_self on public.contractor_trades
    for all to authenticated
    using (public.is_current_contractor(contractor_id))
    with check (public.is_current_contractor(contractor_id));

drop policy if exists landlord_contractors_select_related on public.landlord_contractors;
create policy landlord_contractors_select_related on public.landlord_contractors
    for select to authenticated
    using (
        landlord_id = auth.uid()
        or public.is_current_contractor(contractor_id)
    );

drop policy if exists contractor_unit_access_landlord_select on public.contractor_unit_access;
create policy contractor_unit_access_landlord_select on public.contractor_unit_access
    for select to authenticated
    using (landlord_id = auth.uid() and public.is_landlord_user());

drop policy if exists contractor_invites_landlord_access on public.contractor_invites;
create policy contractor_invites_landlord_access on public.contractor_invites
    for all to authenticated
    using (landlord_id = auth.uid() and public.is_landlord_user())
    with check (landlord_id = auth.uid() and public.is_landlord_user());

drop policy if exists unit_invites_landlord_access on public.unit_invites;
create policy unit_invites_landlord_access on public.unit_invites
    for all to authenticated
    using (public.is_landlord_of_unit(unit_id))
    with check (public.is_landlord_of_unit(unit_id));

drop policy if exists reassignment_log_landlord_select on public.reassignment_log;
create policy reassignment_log_landlord_select on public.reassignment_log
    for select to authenticated
    using (exists (
        select 1 from public.issues i
        where i.id = issue_id and i.landlord_id = auth.uid() and public.is_landlord_user()
    ));

drop policy if exists landlord_notifications_select on public.landlord_notifications;
create policy landlord_notifications_select on public.landlord_notifications
    for select to authenticated
    using (landlord_id = auth.uid() and public.is_landlord_user());
drop policy if exists landlord_notifications_update on public.landlord_notifications;
create policy landlord_notifications_update on public.landlord_notifications
    for update to authenticated
    using (landlord_id = auth.uid() and public.is_landlord_user())
    with check (landlord_id = auth.uid() and public.is_landlord_user());

revoke update on public.contractors from anon, authenticated;
grant select on public.contractors to authenticated;
grant update (display_name, contact_details, service_area) on public.contractors to authenticated;
grant select, insert, update, delete on public.contractor_trades to authenticated;
grant select on public.landlord_contractors, public.contractor_unit_access, public.reassignment_log to authenticated;
grant select, insert on public.contractor_invites, public.unit_invites to authenticated;
grant select on public.landlord_notifications to authenticated;
revoke insert, update, delete, truncate on public.landlord_contractors, public.reassignment_log from anon, authenticated;
revoke update, delete, truncate on public.contractor_invites, public.unit_invites from anon, authenticated;
revoke insert, update, delete, truncate on public.landlord_notifications from anon, authenticated;

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
            or exists (
                select 1 from public.media m
                where m.storage_path = name and public.can_access_issue(m.issue_id)
            )
            or exists (
                select 1 from public.issue_quotes q
                where q.storage_path = name and public.can_access_issue(q.issue_id)
            )
            or public.can_read_contractor_media(name)
        )
    );

do $$
begin
    if exists (select 1 from pg_catalog.pg_extension where extname = 'pg_cron') then
        execute 'select cron.unschedule(jobid) from cron.job where jobname = ''complete-stale-contractor-work''';
        execute 'select cron.schedule(''complete-stale-contractor-work'', ''0 * * * *'', ''select public.complete_stale_contractor_work();'')';
    end if;
end;
$$;
