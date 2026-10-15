-- WARNING: This schema is for context only and is not meant to be run.
-- Table order and constraints may not be valid for execution.

CREATE TABLE public.users (
  id uuid NOT NULL,
  full_name text NOT NULL,
  email text NOT NULL UNIQUE,
  phone_number text,
  role USER-DEFINED NOT NULL DEFAULT 'TENANT'::user_role,
  created_at timestamp with time zone DEFAULT now(),
  CONSTRAINT users_pkey PRIMARY KEY (id),
  CONSTRAINT users_id_fkey FOREIGN KEY (id) REFERENCES auth.users(id)
);
CREATE TABLE public.contractor_profiles (
  id uuid NOT NULL DEFAULT gen_random_uuid(),
  user_id uuid NOT NULL UNIQUE,
  company_name text NOT NULL,
  trade USER-DEFINED NOT NULL,
  tax_vat_number text,
  bank_account_details jsonb NOT NULL DEFAULT '{}'::jsonb,
  is_verified boolean DEFAULT false,
  average_rating numeric DEFAULT 0.00,
  created_at timestamp with time zone DEFAULT now(),
  CONSTRAINT contractor_profiles_pkey PRIMARY KEY (id),
  CONSTRAINT contractor_profiles_user_id_fkey FOREIGN KEY (user_id) REFERENCES public.users(id)
);
CREATE TABLE public.units (
  id uuid NOT NULL DEFAULT gen_random_uuid(),
  landlord_id uuid NOT NULL,
  unit_number text NOT NULL,
  building_name text NOT NULL,
  street_address text NOT NULL,
  monthly_rent numeric NOT NULL,
  deposit_amount numeric NOT NULL,
  created_at timestamp with time zone DEFAULT now(),
  CONSTRAINT units_pkey PRIMARY KEY (id),
  CONSTRAINT units_landlord_id_fkey FOREIGN KEY (landlord_id) REFERENCES public.users(id)
);
CREATE TABLE public.tenancies (
  id uuid NOT NULL DEFAULT gen_random_uuid(),
  unit_id uuid NOT NULL,
  primary_tenant_id uuid NOT NULL,
  start_date date NOT NULL,
  end_date date NOT NULL,
  is_active boolean DEFAULT true,
  created_at timestamp with time zone DEFAULT now(),
  CONSTRAINT tenancies_pkey PRIMARY KEY (id),
  CONSTRAINT tenancies_unit_id_fkey FOREIGN KEY (unit_id) REFERENCES public.units(id),
  CONSTRAINT tenancies_primary_tenant_id_fkey FOREIGN KEY (primary_tenant_id) REFERENCES public.users(id)
);
CREATE TABLE public.tenancy_co_tenants (
  tenancy_id uuid NOT NULL,
  tenant_id uuid NOT NULL,
  rent_share_percentage numeric DEFAULT 100.00,
  CONSTRAINT tenancy_co_tenants_pkey PRIMARY KEY (tenancy_id, tenant_id),
  CONSTRAINT tenancy_co_tenants_tenancy_id_fkey FOREIGN KEY (tenancy_id) REFERENCES public.tenancies(id),
  CONSTRAINT tenancy_co_tenants_tenant_id_fkey FOREIGN KEY (tenant_id) REFERENCES public.users(id)
);
CREATE TABLE public.payments (
  id uuid NOT NULL DEFAULT gen_random_uuid(),
  tenancy_id uuid NOT NULL,
  payer_id uuid NOT NULL,
  amount numeric NOT NULL,
  description text NOT NULL,
  method USER-DEFINED NOT NULL,
  status USER-DEFINED NOT NULL DEFAULT 'PENDING'::payment_status,
  gateway_reference text,
  paid_at timestamp with time zone,
  created_at timestamp with time zone DEFAULT now(),
  CONSTRAINT payments_pkey PRIMARY KEY (id),
  CONSTRAINT payments_tenancy_id_fkey FOREIGN KEY (tenancy_id) REFERENCES public.tenancies(id),
  CONSTRAINT payments_payer_id_fkey FOREIGN KEY (payer_id) REFERENCES public.users(id)
);
CREATE TABLE public.utility_meters (
  id uuid NOT NULL DEFAULT gen_random_uuid(),
  unit_id uuid NOT NULL,
  meter_number text NOT NULL UNIQUE,
  meter_type text NOT NULL,
  vendor_provider text NOT NULL,
  CONSTRAINT utility_meters_pkey PRIMARY KEY (id),
  CONSTRAINT utility_meters_unit_id_fkey FOREIGN KEY (unit_id) REFERENCES public.units(id)
);
CREATE TABLE public.utility_tokens (
  id uuid NOT NULL DEFAULT gen_random_uuid(),
  meter_id uuid NOT NULL,
  payment_id uuid NOT NULL,
  token_code text NOT NULL,
  units_purchased numeric NOT NULL,
  created_at timestamp with time zone DEFAULT now(),
  CONSTRAINT utility_tokens_pkey PRIMARY KEY (id),
  CONSTRAINT utility_tokens_meter_id_fkey FOREIGN KEY (meter_id) REFERENCES public.utility_meters(id),
  CONSTRAINT utility_tokens_payment_id_fkey FOREIGN KEY (payment_id) REFERENCES public.payments(id)
);
CREATE TABLE public.work_orders (
  id uuid NOT NULL DEFAULT gen_random_uuid(),
  unit_id uuid NOT NULL,
  logged_by_id uuid NOT NULL,
  assigned_contractor_id uuid,
  category text NOT NULL,
  priority USER-DEFINED NOT NULL DEFAULT 'MEDIUM'::work_order_priority,
  status USER-DEFINED NOT NULL DEFAULT 'LOGGED'::work_order_status,
  title text NOT NULL,
  description text NOT NULL,
  scheduled_access_time timestamp with time zone,
  created_at timestamp with time zone DEFAULT now(),
  CONSTRAINT work_orders_pkey PRIMARY KEY (id),
  CONSTRAINT work_orders_unit_id_fkey FOREIGN KEY (unit_id) REFERENCES public.units(id),
  CONSTRAINT work_orders_logged_by_id_fkey FOREIGN KEY (logged_by_id) REFERENCES public.users(id),
  CONSTRAINT work_orders_assigned_contractor_id_fkey FOREIGN KEY (assigned_contractor_id) REFERENCES public.contractor_profiles(id)
);
CREATE TABLE public.contractor_quotes (
  id uuid NOT NULL DEFAULT gen_random_uuid(),
  work_order_id uuid NOT NULL,
  contractor_id uuid NOT NULL,
  labor_cost numeric NOT NULL,
  material_cost numeric NOT NULL,
  total_amount numeric DEFAULT (labor_cost + material_cost),
  estimated_hours numeric,
  notes text,
  status USER-DEFINED NOT NULL DEFAULT 'SUBMITTED'::quote_status,
  created_at timestamp with time zone DEFAULT now(),
  CONSTRAINT contractor_quotes_pkey PRIMARY KEY (id),
  CONSTRAINT contractor_quotes_work_order_id_fkey FOREIGN KEY (work_order_id) REFERENCES public.work_orders(id),
  CONSTRAINT contractor_quotes_contractor_id_fkey FOREIGN KEY (contractor_id) REFERENCES public.contractor_profiles(id)
);
CREATE TABLE public.job_proof_media (
  id uuid NOT NULL DEFAULT gen_random_uuid(),
  work_order_id uuid NOT NULL,
  uploaded_by_id uuid NOT NULL,
  proof_type text NOT NULL,
  media_url text NOT NULL,
  latitude numeric,
  longitude numeric,
  created_at timestamp with time zone DEFAULT now(),
  CONSTRAINT job_proof_media_pkey PRIMARY KEY (id),
  CONSTRAINT job_proof_media_work_order_id_fkey FOREIGN KEY (work_order_id) REFERENCES public.work_orders(id),
  CONSTRAINT job_proof_media_uploaded_by_id_fkey FOREIGN KEY (uploaded_by_id) REFERENCES public.users(id)
);
CREATE TABLE public.contractor_invoices (
  id uuid NOT NULL DEFAULT gen_random_uuid(),
  work_order_id uuid NOT NULL,
  contractor_id uuid NOT NULL,
  invoice_number text NOT NULL UNIQUE,
  total_amount numeric NOT NULL,
  is_approved_by_landlord boolean DEFAULT false,
  payout_status USER-DEFINED NOT NULL DEFAULT 'PENDING'::payment_status,
  created_at timestamp with time zone DEFAULT now(),
  CONSTRAINT contractor_invoices_pkey PRIMARY KEY (id),
  CONSTRAINT contractor_invoices_work_order_id_fkey FOREIGN KEY (work_order_id) REFERENCES public.work_orders(id),
  CONSTRAINT contractor_invoices_contractor_id_fkey FOREIGN KEY (contractor_id) REFERENCES public.contractor_profiles(id)
);
CREATE TABLE public.tenancy_invites (
  id uuid NOT NULL DEFAULT gen_random_uuid(),
  unit_id uuid NOT NULL,
  landlord_id uuid NOT NULL,
  tenant_email text NOT NULL,
  tenant_id uuid,
  status text NOT NULL DEFAULT 'PENDING',
  start_date date NOT NULL,
  end_date date NOT NULL,
  created_at timestamp with time zone DEFAULT now(),
  responded_at timestamp with time zone,
  CONSTRAINT tenancy_invites_pkey PRIMARY KEY (id),
  CONSTRAINT tenancy_invites_unit_id_fkey FOREIGN KEY (unit_id) REFERENCES public.units(id),
  CONSTRAINT tenancy_invites_landlord_id_fkey FOREIGN KEY (landlord_id) REFERENCES public.users(id)
);
CREATE TABLE public.inspections (
  id uuid NOT NULL DEFAULT gen_random_uuid(),
  tenancy_id uuid NOT NULL,
  inspection_type text NOT NULL,
  checklist_data jsonb NOT NULL DEFAULT '{}'::jsonb,
  tenant_signed_at timestamp with time zone,
  landlord_signed_at timestamp with time zone,
  created_at timestamp with time zone DEFAULT now(),
  CONSTRAINT inspections_pkey PRIMARY KEY (id),
  CONSTRAINT inspections_tenancy_id_fkey FOREIGN KEY (tenancy_id) REFERENCES public.tenancies(id)
);