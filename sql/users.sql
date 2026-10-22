-- Users and the yards each person may see. The dev host applies the same statements on startup
-- (UserDirectory.EnsureAsync), so an existing database picks this up without a volume reset.
-- Fresh containers also get it from db/init/01_platform_core.sql.

ALTER TABLE public.sys_users ADD COLUMN IF NOT EXISTS password_hash text;
ALTER TABLE public.sys_users ADD COLUMN IF NOT EXISTS phone varchar(30);
ALTER TABLE public.sys_users ADD COLUMN IF NOT EXISTS is_admin boolean NOT NULL DEFAULT FALSE;
ALTER TABLE public.sys_users ADD COLUMN IF NOT EXISTS created_on timestamp NOT NULL DEFAULT NOW();
ALTER TABLE public.sys_users ADD COLUMN IF NOT EXISTS updated_on timestamp NOT NULL DEFAULT NOW();

CREATE UNIQUE INDEX IF NOT EXISTS uq_sys_users_email ON public.sys_users (LOWER(email));

CREATE SEQUENCE IF NOT EXISTS public.sys_users_id_seq AS bigint;
SELECT setval(
    'public.sys_users_id_seq',
    (SELECT COALESCE(MAX(id), 1) FROM public.sys_users),
    true);

CREATE TABLE IF NOT EXISTS public.sys_user_yard (
    user_id        bigint NOT NULL REFERENCES public.sys_users (id) ON DELETE CASCADE,
    project_id     bigint NOT NULL,
    sub_project_id bigint NOT NULL,
    yard_type_id   bigint NOT NULL,
    PRIMARY KEY (user_id, project_id, sub_project_id, yard_type_id)
);
