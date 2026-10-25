-- Permit a dispatch link to read only its assigned work order after validating its PIN.
-- Run in the Supabase SQL Editor as the project owner.

drop function if exists public.get_contractor_work_order_by_dispatch_pin(uuid, text);

create function public.get_contractor_work_order_by_dispatch_pin(
    p_work_order_id uuid,
    p_dispatch_pin text
)
returns jsonb
language sql
stable
security definer
set search_path = ''
as $$
    select to_jsonb(work_order)
    from public.work_orders as work_order
    where work_order.id = p_work_order_id
      and work_order.assigned_contractor_id is not null
      and length(trim(coalesce(p_dispatch_pin, ''))) = 4
      and upper(trim(p_dispatch_pin)) =
          upper(left(replace(work_order.id::text, '-', ''), 4));
$$;

revoke all on function public.get_contractor_work_order_by_dispatch_pin(uuid, text) from public;
grant execute on function public.get_contractor_work_order_by_dispatch_pin(uuid, text) to anon, authenticated;
