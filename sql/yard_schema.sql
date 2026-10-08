-- Yard Management tables, for a local Postgres during pre-development.
-- Generated from backend/Queries/Yard/YardDdl.cs: replace {schema} with any schema name,
-- e.g.  sed 's/{schema}/yard_dev/g' yard_schema.sql | psql -d primworth_dev
-- The platform never runs this file; YardProvisioner creates the same tables per facility.

CREATE SCHEMA IF NOT EXISTS "{schema}";

-- CreateMaterialType
CREATE TABLE IF NOT EXISTS "{schema}".sp_yard_material_type (
    material_type_id bigint GENERATED ALWAYS AS IDENTITY,
    type_key         varchar(32)  NOT NULL,
    singular         varchar(50)  NOT NULL,
    plural           varchar(50)  NOT NULL,
    id_label         varchar(50)  NOT NULL,
    position_label   varchar(50)  NOT NULL DEFAULT 'R{row}-C{col}',
    attribute_defs   jsonb        NOT NULL DEFAULT '[]'::jsonb,
    rule_set_key     varchar(64),
    is_active        boolean   NOT NULL DEFAULT TRUE,
    is_deleted       boolean   NOT NULL DEFAULT FALSE,
    created_by       bigint,
    created_on       timestamp NOT NULL DEFAULT NOW(),
    updated_by       bigint,
    updated_on       timestamp NOT NULL DEFAULT NOW(),
    CONSTRAINT pk_sp_yard_material_type PRIMARY KEY (material_type_id)
);
CREATE UNIQUE INDEX IF NOT EXISTS uq_sp_yard_material_type_type_key
    ON "{schema}".sp_yard_material_type (LOWER(type_key)) WHERE is_deleted = FALSE;

-- CreateZone
CREATE TABLE IF NOT EXISTS "{schema}".sp_yard_zone (
    zone_id       bigint GENERATED ALWAYS AS IDENTITY,
    zone_name     varchar(100) NOT NULL,
    zone_purpose  varchar(20)  NOT NULL DEFAULT 'STORAGE',
    storage_style varchar(10)  NOT NULL DEFAULT 'STACK',
    is_active     boolean   NOT NULL DEFAULT TRUE,
    is_deleted    boolean   NOT NULL DEFAULT FALSE,
    created_by    bigint,
    created_on    timestamp NOT NULL DEFAULT NOW(),
    updated_by    bigint,
    updated_on    timestamp NOT NULL DEFAULT NOW(),
    CONSTRAINT pk_sp_yard_zone PRIMARY KEY (zone_id),
    CONSTRAINT ck_sp_yard_zone_purpose
        CHECK (zone_purpose IN ('STORAGE', 'REWORK', 'DISPATCH', 'SHUFFLING')),
    CONSTRAINT ck_sp_yard_zone_storage_style
        CHECK (storage_style IN ('STACK', 'RACK', 'FLOOR', 'PYRAMID'))
);
CREATE UNIQUE INDEX IF NOT EXISTS uq_sp_yard_zone_zone_name
    ON "{schema}".sp_yard_zone (LOWER(zone_name)) WHERE is_deleted = FALSE;

-- CreateZoneMaterialType
CREATE TABLE IF NOT EXISTS "{schema}".sp_yard_zone_material_type (
    zone_id          bigint NOT NULL,
    material_type_id bigint NOT NULL,
    CONSTRAINT pk_sp_yard_zone_material_type PRIMARY KEY (zone_id, material_type_id),
    CONSTRAINT fk_sp_yard_zone_material_type_zone_id
        FOREIGN KEY (zone_id) REFERENCES "{schema}".sp_yard_zone (zone_id) ON DELETE CASCADE,
    CONSTRAINT fk_sp_yard_zone_material_type_material_type_id
        FOREIGN KEY (material_type_id) REFERENCES "{schema}".sp_yard_material_type (material_type_id)
);

-- CreatePosition
CREATE TABLE IF NOT EXISTS "{schema}".sp_yard_position (
    position_id    bigint GENERATED ALWAYS AS IDENTITY,
    zone_id        bigint  NOT NULL,
    row_index      integer NOT NULL,
    col_index      integer NOT NULL,
    capacity_limit integer NOT NULL DEFAULT 0,
    is_deleted     boolean NOT NULL DEFAULT FALSE,
    created_by     bigint,
    created_on     timestamp NOT NULL DEFAULT NOW(),
    updated_by     bigint,
    updated_on     timestamp NOT NULL DEFAULT NOW(),
    CONSTRAINT pk_sp_yard_position PRIMARY KEY (position_id),
    CONSTRAINT fk_sp_yard_position_zone_id
        FOREIGN KEY (zone_id) REFERENCES "{schema}".sp_yard_zone (zone_id),
    CONSTRAINT uq_sp_yard_position_grid UNIQUE (zone_id, row_index, col_index)
);

-- CreateItem
CREATE TABLE IF NOT EXISTS "{schema}".sp_yard_item (
    item_id          bigint GENERATED ALWAYS AS IDENTITY,
    item_code        varchar(64) NOT NULL,
    heat_number      varchar(64),
    material_type_id bigint NOT NULL,
    grade_code       varchar(256),
    weight_kg        numeric(12,3),
    attributes       jsonb NOT NULL DEFAULT '{}'::jsonb,
    item_status      varchar(20) NOT NULL DEFAULT 'PENDING',
    quality_status   varchar(20) NOT NULL DEFAULT 'PENDING',
    zone_id          bigint,
    position_id      bigint,
    stack_index      integer,
    arrived_on       timestamp NOT NULL DEFAULT NOW(),
    is_virtual       boolean NOT NULL DEFAULT FALSE,
    is_deleted       boolean NOT NULL DEFAULT FALSE,
    created_by       bigint,
    created_on       timestamp NOT NULL DEFAULT NOW(),
    updated_by       bigint,
    updated_on       timestamp NOT NULL DEFAULT NOW(),
    CONSTRAINT pk_sp_yard_item PRIMARY KEY (item_id),
    CONSTRAINT fk_sp_yard_item_material_type_id
        FOREIGN KEY (material_type_id) REFERENCES "{schema}".sp_yard_material_type (material_type_id),
    CONSTRAINT fk_sp_yard_item_zone_id
        FOREIGN KEY (zone_id) REFERENCES "{schema}".sp_yard_zone (zone_id),
    CONSTRAINT fk_sp_yard_item_position_id
        FOREIGN KEY (position_id) REFERENCES "{schema}".sp_yard_position (position_id),
    CONSTRAINT ck_sp_yard_item_item_status
        CHECK (item_status IN ('PENDING','READY','HOLD','REWORK','SCRAPPED','DISPATCHED')),
    CONSTRAINT ck_sp_yard_item_quality_status
        CHECK (quality_status IN ('PENDING','APPROVED','LOCKED_QA'))
);
CREATE UNIQUE INDEX IF NOT EXISTS uq_sp_yard_item_item_code
    ON "{schema}".sp_yard_item (LOWER(item_code)) WHERE is_deleted = FALSE;
-- Two live items can never share one place in a position. This is what makes a
-- stack honest; in a rack (capacity 1) it makes a double booking impossible.
CREATE UNIQUE INDEX IF NOT EXISTS uq_sp_yard_item_position_stack_index
    ON "{schema}".sp_yard_item (position_id, stack_index)
    WHERE is_deleted = FALSE AND position_id IS NOT NULL AND stack_index IS NOT NULL;
CREATE INDEX IF NOT EXISTS ix_sp_yard_item_zone
    ON "{schema}".sp_yard_item (zone_id) WHERE is_deleted = FALSE;
CREATE INDEX IF NOT EXISTS ix_sp_yard_item_material_type
    ON "{schema}".sp_yard_item (material_type_id) WHERE is_deleted = FALSE;

-- CreateWorkOrder
CREATE TABLE IF NOT EXISTS "{schema}".sp_yard_work_order (
    work_order_id           bigint GENERATED ALWAYS AS IDENTITY,
    order_type              varchar(20) NOT NULL,
    item_id                 bigint NOT NULL,
    source_zone_id          bigint,
    source_position_id      bigint,
    destination_zone_id     bigint,
    destination_position_id bigint,
    order_status            varchar(20) NOT NULL DEFAULT 'PENDING',
    -- public.sys_users.id; deliberately not a foreign key, public is authoritative.
    assigned_user_id        bigint,
    flag_reason             text,
    completed_at            timestamp,
    is_deleted              boolean NOT NULL DEFAULT FALSE,
    created_by              bigint,
    created_on              timestamp NOT NULL DEFAULT NOW(),
    updated_by              bigint,
    updated_on              timestamp NOT NULL DEFAULT NOW(),
    CONSTRAINT pk_sp_yard_work_order PRIMARY KEY (work_order_id),
    CONSTRAINT fk_sp_yard_work_order_item_id
        FOREIGN KEY (item_id) REFERENCES "{schema}".sp_yard_item (item_id),
    CONSTRAINT fk_sp_yard_work_order_source_zone_id
        FOREIGN KEY (source_zone_id) REFERENCES "{schema}".sp_yard_zone (zone_id),
    CONSTRAINT fk_sp_yard_work_order_source_position_id
        FOREIGN KEY (source_position_id) REFERENCES "{schema}".sp_yard_position (position_id),
    CONSTRAINT fk_sp_yard_work_order_destination_zone_id
        FOREIGN KEY (destination_zone_id) REFERENCES "{schema}".sp_yard_zone (zone_id),
    CONSTRAINT fk_sp_yard_work_order_destination_position_id
        FOREIGN KEY (destination_position_id) REFERENCES "{schema}".sp_yard_position (position_id),
    CONSTRAINT ck_sp_yard_work_order_order_type
        CHECK (order_type IN ('INBOUND','MOVE','DIGGING','REWORK','DISPATCH','AUDIT')),
    CONSTRAINT ck_sp_yard_work_order_order_status
        CHECK (order_status IN ('PENDING','IN_PROGRESS','COMPLETED','FLAGGED'))
);
CREATE INDEX IF NOT EXISTS ix_sp_yard_work_order_status_created
    ON "{schema}".sp_yard_work_order (order_status, created_on);
CREATE INDEX IF NOT EXISTS ix_sp_yard_work_order_item
    ON "{schema}".sp_yard_work_order (item_id);

-- CreateMovementLog
CREATE TABLE IF NOT EXISTS "{schema}".sp_yard_movement_log (
    movement_log_id bigint GENERATED ALWAYS AS IDENTITY,
    item_id         bigint NOT NULL,
    from_zone_id    bigint,
    to_zone_id      bigint,
    from_zone_name  varchar(100),
    to_zone_name    varchar(100),
    from_position   varchar(32),
    to_position     varchar(32),
    order_type      varchar(20),
    performed_by    bigint,
    performed_name  varchar(120),
    log_timestamp   timestamp NOT NULL DEFAULT NOW(),
    CONSTRAINT pk_sp_yard_movement_log PRIMARY KEY (movement_log_id),
    CONSTRAINT fk_sp_yard_movement_log_item_id
        FOREIGN KEY (item_id) REFERENCES "{schema}".sp_yard_item (item_id)
);
CREATE INDEX IF NOT EXISTS ix_sp_yard_movement_log_item_time
    ON "{schema}".sp_yard_movement_log (item_id, log_timestamp DESC);

-- CreateSystemConfig
CREATE TABLE IF NOT EXISTS "{schema}".sp_yard_system_config (
    config_key   varchar(64) NOT NULL,
    config_value text,
    created_by   bigint,
    created_on   timestamp NOT NULL DEFAULT NOW(),
    updated_by   bigint,
    updated_on   timestamp NOT NULL DEFAULT NOW(),
    CONSTRAINT pk_sp_yard_system_config PRIMARY KEY (config_key)
);

-- The SLAB material type the provisioner seeds
INSERT INTO "{schema}".sp_yard_material_type (type_key, singular, plural, id_label, position_label, attribute_defs, rule_set_key)
SELECT 'SLAB', 'Slab', 'Slabs', 'Slab ID', 'Pit R{row}-C{col}',
       '[{"key":"length_mm","label":"Length","type":"number","unit":"mm","required":false},
         {"key":"width_mm","label":"Width","type":"number","unit":"mm","required":false},
         {"key":"thickness_mm","label":"Thickness","type":"number","unit":"mm","required":false},
         {"key":"cast_time","label":"Cast time","type":"datetime","required":false},
         {"key":"rolling_width_mm","label":"Rolling width","type":"number","unit":"mm","required":false},
         {"key":"target_reheat_c","label":"Reheat target","type":"integer","unit":"C","required":false}]'::jsonb,
       'slab-routing'
WHERE NOT EXISTS (SELECT 1 FROM "{schema}".sp_yard_material_type WHERE type_key = 'SLAB');

INSERT INTO "{schema}".sp_yard_system_config (config_key, config_value)
SELECT k, v FROM (VALUES ('rule_grade_separation', 'false'), ('max_stack_height', '5')) AS s(k, v)
WHERE NOT EXISTS (SELECT 1 FROM "{schema}".sp_yard_system_config c WHERE c.config_key = s.k);
