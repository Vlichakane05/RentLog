-- Fix the Auth signup trigger for the project's uppercase user_role enum.
-- The existing on_auth_user_created trigger calls public.handle_new_user().
-- Run this in the Supabase SQL Editor as the project owner.
create or replace function public.handle_new_user()
returns trigger
language plpgsql
security definer
set search_path = ''
as $$
declare
    profile_role public.user_role;
begin
    profile_role := upper(
        coalesce(nullif(trim(new.raw_user_meta_data ->> 'role'), ''), 'TENANT')
    )::public.user_role;

    insert into public.users (id, email, full_name, role)
    values (
        new.id,
        coalesce(new.email, ''),
        coalesce(
            nullif(new.raw_user_meta_data ->> 'full_name', ''),
            split_part(coalesce(new.email, 'Tenant'), '@', 1)
        ),
        profile_role
    );

    return new;
end;
$$;
