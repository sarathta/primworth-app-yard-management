-- The platform's core tables, as an application sees them: enough routing data for the dev host
-- to resolve schemas, and the masters an application may reference. Shapes match the platform.
-- Runs once, when the container's data volume is first created.

-- ── public: routing ───────────────────────────────────────────────────────────────────────────
-- People who sign in. password_hash is filled by the dev host on startup (the seed row has none).
-- is_admin may open Users and assign yards. role_id 1 is an administrator, 2 is a regular user.
CREATE TABLE public.sys_users (
    id            bigint PRIMARY KEY,
    first_name    varchar NOT NULL,
    last_name     varchar NOT NULL,
    email         varchar NOT NULL,
    role_id       integer NOT NULL,
    is_active     boolean NOT NULL DEFAULT TRUE,
    password_hash text,
    phone         varchar(30),
    is_admin      boolean NOT NULL DEFAULT FALSE,
    created_on    timestamp NOT NULL DEFAULT NOW(),
    updated_on    timestamp NOT NULL DEFAULT NOW()
);
INSERT INTO public.sys_users (id, first_name, last_name, email, role_id, is_active, is_admin)
VALUES (1, 'Dev', 'User', 'dev@local', 1, TRUE, TRUE);
CREATE UNIQUE INDEX uq_sys_users_email ON public.sys_users (LOWER(email));
CREATE SEQUENCE public.sys_users_id_seq AS bigint;
SELECT setval('public.sys_users_id_seq', (SELECT COALESCE(MAX(id), 1) FROM public.sys_users));

-- Yards a person sees in the left menu. yard_type_id is sp_yard_type in that sub-project schema.
CREATE TABLE public.sys_user_yard (
    user_id        bigint NOT NULL REFERENCES public.sys_users (id) ON DELETE CASCADE,
    project_id     bigint NOT NULL,
    sub_project_id bigint NOT NULL,
    yard_type_id   bigint NOT NULL,
    PRIMARY KEY (user_id, project_id, sub_project_id, yard_type_id)
);

CREATE TABLE public.sys_pr_project (
    project_id          bigint PRIMARY KEY,
    project_name        varchar NOT NULL,
    location            varchar,
    oem_id              bigint NOT NULL DEFAULT 1,
    project_description text
);
INSERT INTO public.sys_pr_project (project_id, project_name, location, project_description)
VALUES (1, 'Dev Plant', 'Local', 'Sample plant for application development');

CREATE TABLE public.application_scope (scope_id integer PRIMARY KEY, scope varchar NOT NULL);
INSERT INTO public.application_scope VALUES (1, 'Project'), (2, 'Sub Project'), (3, 'Both');

CREATE TABLE public.application (
    id                   bigint PRIMARY KEY,
    application_name     varchar NOT NULL,
    app_key              varchar,
    is_integrated        boolean NOT NULL DEFAULT FALSE,
    route_path           varchar,
    permission_code      varchar,
    module_table_prefix  varchar,
    provisioner_key      varchar,
    application_scope_id integer REFERENCES public.application_scope (scope_id),
    nav_children         jsonb,
    is_deleted           boolean NOT NULL DEFAULT FALSE
);

-- ── project schema: what every project schema carries ─────────────────────────────────────────
CREATE SCHEMA "1_dev_plant_db";

CREATE TABLE "1_dev_plant_db".gm_facility_type (
    facility_type_id   bigint GENERATED ALWAYS AS IDENTITY PRIMARY KEY,
    facility_type_name varchar NOT NULL,
    description        varchar,
    created_by bigint, created_on timestamp DEFAULT NOW(), updated_by bigint, updated_on timestamp DEFAULT NOW()
);
INSERT INTO "1_dev_plant_db".gm_facility_type (facility_type_name, description)
VALUES ('Steel Melt Shop', 'EAF / LF / CCM route'), ('Wire Rod Mill', 'Rolling');

CREATE TABLE "1_dev_plant_db".gm_equipment_type (
    equipment_type_id          bigint GENERATED ALWAYS AS IDENTITY PRIMARY KEY,
    equipment_type_name        varchar NOT NULL,
    equipment_type_description text NOT NULL DEFAULT '',
    facility_type_id           bigint REFERENCES "1_dev_plant_db".gm_facility_type (facility_type_id),
    created_by bigint, created_on timestamp DEFAULT NOW(), updated_by bigint, updated_on timestamp DEFAULT NOW()
);
INSERT INTO "1_dev_plant_db".gm_equipment_type (equipment_type_name, equipment_type_description, facility_type_id)
VALUES ('Electric Arc Furnace', 'Primary melting', 1), ('Ladle Furnace', 'Secondary metallurgy', 1), ('Continuous Caster', 'Casting', 1);

CREATE TABLE "1_dev_plant_db".gm_grade (
    id          bigint GENERATED ALWAYS AS IDENTITY PRIMARY KEY,
    grade_code  varchar NOT NULL,
    description text,
    created_by bigint, created_on timestamp DEFAULT NOW(), updated_by bigint, updated_on timestamp DEFAULT NOW()
);
INSERT INTO "1_dev_plant_db".gm_grade (grade_code, description)
VALUES ('IS 2062 E250', 'Structural steel'), ('Fe 500D', 'Rebar'), ('EN8', 'Medium carbon');

CREATE TABLE "1_dev_plant_db".sys_sp_sub_project (
    sub_project_id          bigint PRIMARY KEY,
    project_id              bigint NOT NULL,
    project_name            varchar NOT NULL,
    sub_project_name        varchar NOT NULL,
    sub_project_description text,
    facility_type_id        bigint NOT NULL,
    oem_id                  bigint NOT NULL DEFAULT 1
);
INSERT INTO "1_dev_plant_db".sys_sp_sub_project
VALUES (1, 1, 'Dev Plant', 'Melt Shop 1', 'Electric arc furnace route', 1, 1);

-- ── sub-project schema: what every sub-project schema carries ─────────────────────────────────
CREATE SCHEMA "1_dev_plant_1_melt_shop_1_db";

CREATE TABLE "1_dev_plant_1_melt_shop_1_db".gm_facility_type (LIKE "1_dev_plant_db".gm_facility_type INCLUDING ALL);
INSERT INTO "1_dev_plant_1_melt_shop_1_db".gm_facility_type (facility_type_name, description)
SELECT facility_type_name, description FROM "1_dev_plant_db".gm_facility_type;

CREATE TABLE "1_dev_plant_1_melt_shop_1_db".gm_equipment_type (LIKE "1_dev_plant_db".gm_equipment_type INCLUDING ALL);
INSERT INTO "1_dev_plant_1_melt_shop_1_db".gm_equipment_type (equipment_type_name, equipment_type_description, facility_type_id)
SELECT equipment_type_name, equipment_type_description, facility_type_id FROM "1_dev_plant_db".gm_equipment_type;

CREATE TABLE "1_dev_plant_1_melt_shop_1_db".gm_grade (LIKE "1_dev_plant_db".gm_grade INCLUDING ALL);
INSERT INTO "1_dev_plant_1_melt_shop_1_db".gm_grade (grade_code, description)
SELECT grade_code, description FROM "1_dev_plant_db".gm_grade;

-- Equipment instances exist ONLY here, in the sub-project schema — as in the platform.
CREATE TABLE "1_dev_plant_1_melt_shop_1_db".sp_equipment_instance (
    equipment_instance_id bigint GENERATED ALWAYS AS IDENTITY PRIMARY KEY,
    equipment_type_id     bigint REFERENCES "1_dev_plant_1_melt_shop_1_db".gm_equipment_type (equipment_type_id),
    instance_name         varchar,
    instance_description  text,
    created_by bigint, created_on timestamp DEFAULT NOW(), updated_by bigint, updated_on timestamp DEFAULT NOW()
);
INSERT INTO "1_dev_plant_1_melt_shop_1_db".sp_equipment_instance (equipment_type_id, instance_name, instance_description)
VALUES (1, 'EAF-1', '100 t electric arc furnace'), (2, 'LF-1', 'Ladle furnace 1'),
       (3, 'CCM-1', '6-strand billet caster'), (3, 'CCM-2', '4-strand bloom caster');
