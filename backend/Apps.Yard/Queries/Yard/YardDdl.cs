namespace Primworth_API.Queries.Yard
{
    /// <summary>
    /// Every table Yard Management owns inside a facility (sub-project) schema, as idempotent DDL.
    /// "{schema}" is replaced by the provisioner with a name DbSchemaHelper has already sanitised.
    ///
    /// Generic from the first line: an item has a material type and a typed attribute bag, an
    /// area has a storage style and a list of the material types it accepts. A slab plant never
    /// sees any of that; the SLAB material type is seeded and every area defaults to STACK.
    ///
    /// Conventions mirrored from the platform: bigint identity keys, snake_case columns, the four
    /// audit columns, pk_/fk_/uq_/ck_ constraint names, varchar + CHECK instead of enums. Every
    /// statement is IF NOT EXISTS because provisioning runs on create and on every later edit.
    /// </summary>
    public static class YardDdl
    {
        // ── Material type: the record that makes the module generic ─────────────
        // attribute_defs is a JSON array of {key, label, type, unit, required}. YardAttributes
        // validates every item against it, so two facilities cannot spell "width" differently.
        public const string CreateMaterialType = @"
            CREATE TABLE IF NOT EXISTS ""{schema}"".sp_yard_material_type (
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
                ON ""{schema}"".sp_yard_material_type (LOWER(type_key)) WHERE is_deleted = FALSE;";

        // ── Areas and positions ───────────────────────────────────────────────────
        public const string CreateZone = @"
            CREATE TABLE IF NOT EXISTS ""{schema}"".sp_yard_zone (
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
                ON ""{schema}"".sp_yard_zone (LOWER(zone_name)) WHERE is_deleted = FALSE;";

        /// <summary>Which material types an area accepts. An area with no rows accepts nothing.</summary>
        public const string CreateZoneMaterialType = @"
            CREATE TABLE IF NOT EXISTS ""{schema}"".sp_yard_zone_material_type (
                zone_id          bigint NOT NULL,
                material_type_id bigint NOT NULL,
                CONSTRAINT pk_sp_yard_zone_material_type PRIMARY KEY (zone_id, material_type_id),
                CONSTRAINT fk_sp_yard_zone_material_type_zone_id
                    FOREIGN KEY (zone_id) REFERENCES ""{schema}"".sp_yard_zone (zone_id) ON DELETE CASCADE,
                CONSTRAINT fk_sp_yard_zone_material_type_material_type_id
                    FOREIGN KEY (material_type_id) REFERENCES ""{schema}"".sp_yard_material_type (material_type_id)
            );";

        /// <summary>
        /// One addressable place in an area. STACK: a pit that holds capacity_limit items on top of
        /// each other. RACK: a slot, capacity 1. FLOOR: the area has exactly one position whose
        /// capacity is the whole area. PYRAMID: row_index is the level.
        /// </summary>
        public const string CreatePosition = @"
            CREATE TABLE IF NOT EXISTS ""{schema}"".sp_yard_position (
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
                    FOREIGN KEY (zone_id) REFERENCES ""{schema}"".sp_yard_zone (zone_id),
                CONSTRAINT uq_sp_yard_position_grid UNIQUE (zone_id, row_index, col_index)
            );";

        // ── The item ─────────────────────────────────────────────────────────────
        // Shared columns are what every material has. Everything material-specific lives in
        // attributes, validated against the material type's attribute_defs on the way in.
        // grade_code is a soft reference to the platform's gm_grade.grade_code.
        public const string CreateItem = @"
            CREATE TABLE IF NOT EXISTS ""{schema}"".sp_yard_item (
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
                    FOREIGN KEY (material_type_id) REFERENCES ""{schema}"".sp_yard_material_type (material_type_id),
                CONSTRAINT fk_sp_yard_item_zone_id
                    FOREIGN KEY (zone_id) REFERENCES ""{schema}"".sp_yard_zone (zone_id),
                CONSTRAINT fk_sp_yard_item_position_id
                    FOREIGN KEY (position_id) REFERENCES ""{schema}"".sp_yard_position (position_id),
                CONSTRAINT ck_sp_yard_item_item_status
                    CHECK (item_status IN ('PENDING','READY','HOLD','REWORK','SCRAPPED','DISPATCHED')),
                CONSTRAINT ck_sp_yard_item_quality_status
                    CHECK (quality_status IN ('PENDING','APPROVED','LOCKED_QA'))
            );
            CREATE UNIQUE INDEX IF NOT EXISTS uq_sp_yard_item_item_code
                ON ""{schema}"".sp_yard_item (LOWER(item_code)) WHERE is_deleted = FALSE;
            -- Two live items can never share one place in a position. This is what makes a
            -- stack honest; in a rack (capacity 1) it makes a double booking impossible.
            CREATE UNIQUE INDEX IF NOT EXISTS uq_sp_yard_item_position_stack_index
                ON ""{schema}"".sp_yard_item (position_id, stack_index)
                WHERE is_deleted = FALSE AND position_id IS NOT NULL AND stack_index IS NOT NULL;
            CREATE INDEX IF NOT EXISTS ix_sp_yard_item_zone
                ON ""{schema}"".sp_yard_item (zone_id) WHERE is_deleted = FALSE;
            CREATE INDEX IF NOT EXISTS ix_sp_yard_item_material_type
                ON ""{schema}"".sp_yard_item (material_type_id) WHERE is_deleted = FALSE;";

        // ── Work orders and history ──────────────────────────────────────────────
        public const string CreateWorkOrder = @"
            CREATE TABLE IF NOT EXISTS ""{schema}"".sp_yard_work_order (
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
                    FOREIGN KEY (item_id) REFERENCES ""{schema}"".sp_yard_item (item_id),
                CONSTRAINT fk_sp_yard_work_order_source_zone_id
                    FOREIGN KEY (source_zone_id) REFERENCES ""{schema}"".sp_yard_zone (zone_id),
                CONSTRAINT fk_sp_yard_work_order_source_position_id
                    FOREIGN KEY (source_position_id) REFERENCES ""{schema}"".sp_yard_position (position_id),
                CONSTRAINT fk_sp_yard_work_order_destination_zone_id
                    FOREIGN KEY (destination_zone_id) REFERENCES ""{schema}"".sp_yard_zone (zone_id),
                CONSTRAINT fk_sp_yard_work_order_destination_position_id
                    FOREIGN KEY (destination_position_id) REFERENCES ""{schema}"".sp_yard_position (position_id),
                CONSTRAINT ck_sp_yard_work_order_order_type
                    CHECK (order_type IN ('INBOUND','MOVE','DIGGING','REWORK','DISPATCH','AUDIT')),
                CONSTRAINT ck_sp_yard_work_order_order_status
                    CHECK (order_status IN ('PENDING','IN_PROGRESS','COMPLETED','FLAGGED'))
            );
            CREATE INDEX IF NOT EXISTS ix_sp_yard_work_order_status_created
                ON ""{schema}"".sp_yard_work_order (order_status, created_on);
            CREATE INDEX IF NOT EXISTS ix_sp_yard_work_order_item
                ON ""{schema}"".sp_yard_work_order (item_id);";

        /// <summary>Names are copied in, not joined, so history survives a renamed or deleted area.</summary>
        public const string CreateMovementLog = @"
            CREATE TABLE IF NOT EXISTS ""{schema}"".sp_yard_movement_log (
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
                    FOREIGN KEY (item_id) REFERENCES ""{schema}"".sp_yard_item (item_id)
            );
            CREATE INDEX IF NOT EXISTS ix_sp_yard_movement_log_item_time
                ON ""{schema}"".sp_yard_movement_log (item_id, log_timestamp DESC);";

        // ── Masters the mSYMS screens edit ───────────────────────────────────────
        // Grades are NOT here: the platform's gm_grade is the grade master and is cloned into
        // every facility schema.

        /// <summary>Standard sizes per material. Slab: length, width, thickness. Other materials use attributes.</summary>
        public const string CreateSizeProfile = @"
            CREATE TABLE IF NOT EXISTS ""{schema}"".sp_yard_size_profile (
                size_profile_id  bigint GENERATED ALWAYS AS IDENTITY,
                profile_name     varchar(100)  NOT NULL,
                material_type_id bigint,
                length_mm        numeric(10,2) NOT NULL DEFAULT 0,
                width_mm         numeric(10,2) NOT NULL DEFAULT 0,
                thickness_mm     numeric(10,2) NOT NULL DEFAULT 0,
                tolerance_pct    numeric(5,2)  NOT NULL DEFAULT 0.5,
                attributes       jsonb         NOT NULL DEFAULT '{}'::jsonb,
                is_active        boolean   NOT NULL DEFAULT TRUE,
                is_deleted       boolean   NOT NULL DEFAULT FALSE,
                created_by       bigint,
                created_on       timestamp NOT NULL DEFAULT NOW(),
                updated_by       bigint,
                updated_on       timestamp NOT NULL DEFAULT NOW(),
                CONSTRAINT pk_sp_yard_size_profile PRIMARY KEY (size_profile_id),
                CONSTRAINT fk_sp_yard_size_profile_material_type_id
                    FOREIGN KEY (material_type_id) REFERENCES ""{schema}"".sp_yard_material_type (material_type_id)
            );
            CREATE UNIQUE INDEX IF NOT EXISTS uq_sp_yard_size_profile_profile_name
                ON ""{schema}"".sp_yard_size_profile (LOWER(profile_name)) WHERE is_deleted = FALSE;";

        public const string CreateReworkActivity = @"
            CREATE TABLE IF NOT EXISTS ""{schema}"".sp_yard_rework_activity (
                rework_activity_id bigint GENERATED ALWAYS AS IDENTITY,
                activity_name      varchar(100) NOT NULL,
                description        text,
                is_active          boolean NOT NULL DEFAULT TRUE,
                is_deleted         boolean NOT NULL DEFAULT FALSE,
                created_by         bigint,
                created_on         timestamp NOT NULL DEFAULT NOW(),
                updated_by         bigint,
                updated_on         timestamp NOT NULL DEFAULT NOW(),
                CONSTRAINT pk_sp_yard_rework_activity PRIMARY KEY (rework_activity_id)
            );
            CREATE UNIQUE INDEX IF NOT EXISTS uq_sp_yard_rework_activity_activity_name
                ON ""{schema}"".sp_yard_rework_activity (LOWER(activity_name)) WHERE is_deleted = FALSE;";

        /// <summary>
        /// Product mix: which grade and size combinations route to which area purpose. The
        /// allowed grades and sizes are child tables, so the Masters UI can manage them.
        /// </summary>
        public const string CreateProductMix = @"
            CREATE TABLE IF NOT EXISTS ""{schema}"".sp_yard_product_mix (
                product_mix_id      bigint GENERATED ALWAYS AS IDENTITY,
                mix_name            varchar(100) NOT NULL,
                description         text,
                material_type_id    bigint,
                target_zone_purpose varchar(20) NOT NULL DEFAULT 'STORAGE',
                priority            integer NOT NULL DEFAULT 0,
                is_active           boolean NOT NULL DEFAULT TRUE,
                is_deleted          boolean NOT NULL DEFAULT FALSE,
                created_by          bigint,
                created_on          timestamp NOT NULL DEFAULT NOW(),
                updated_by          bigint,
                updated_on          timestamp NOT NULL DEFAULT NOW(),
                CONSTRAINT pk_sp_yard_product_mix PRIMARY KEY (product_mix_id),
                CONSTRAINT ck_sp_yard_product_mix_target_zone_purpose
                    CHECK (target_zone_purpose IN ('STORAGE', 'REWORK', 'DISPATCH', 'SHUFFLING'))
            );
            CREATE UNIQUE INDEX IF NOT EXISTS uq_sp_yard_product_mix_mix_name
                ON ""{schema}"".sp_yard_product_mix (LOWER(mix_name)) WHERE is_deleted = FALSE;
            CREATE TABLE IF NOT EXISTS ""{schema}"".sp_yard_product_mix_grade (
                id             bigint GENERATED ALWAYS AS IDENTITY,
                product_mix_id bigint       NOT NULL,
                grade_code     varchar(256) NOT NULL,
                CONSTRAINT pk_sp_yard_product_mix_grade PRIMARY KEY (id),
                CONSTRAINT fk_sp_yard_product_mix_grade_product_mix_id
                    FOREIGN KEY (product_mix_id) REFERENCES ""{schema}"".sp_yard_product_mix (product_mix_id) ON DELETE CASCADE,
                CONSTRAINT uq_sp_yard_product_mix_grade UNIQUE (product_mix_id, grade_code)
            );
            CREATE TABLE IF NOT EXISTS ""{schema}"".sp_yard_product_mix_size (
                id              bigint GENERATED ALWAYS AS IDENTITY,
                product_mix_id  bigint NOT NULL,
                size_profile_id bigint NOT NULL,
                CONSTRAINT pk_sp_yard_product_mix_size PRIMARY KEY (id),
                CONSTRAINT fk_sp_yard_product_mix_size_product_mix_id
                    FOREIGN KEY (product_mix_id) REFERENCES ""{schema}"".sp_yard_product_mix (product_mix_id) ON DELETE CASCADE,
                CONSTRAINT fk_sp_yard_product_mix_size_size_profile_id
                    FOREIGN KEY (size_profile_id) REFERENCES ""{schema}"".sp_yard_size_profile (size_profile_id),
                CONSTRAINT uq_sp_yard_product_mix_size UNIQUE (product_mix_id, size_profile_id)
            );";

        /// <summary>User and system actions, for the Configurator's activity log.</summary>
        public const string CreateSystemLog = @"
            CREATE TABLE IF NOT EXISTS ""{schema}"".sp_yard_system_log (
                system_log_id bigint GENERATED ALWAYS AS IDENTITY,
                user_id       bigint,
                action        varchar(64) NOT NULL,
                details       jsonb,
                log_timestamp timestamp NOT NULL DEFAULT NOW(),
                CONSTRAINT pk_sp_yard_system_log PRIMARY KEY (system_log_id)
            );
            CREATE INDEX IF NOT EXISTS ix_sp_yard_system_log_time
                ON ""{schema}"".sp_yard_system_log (log_timestamp DESC);";

        /// <summary>Columns added after the first cut; harmless on a fresh schema.</summary>
        public const string AddLaterColumns = @"
            ALTER TABLE ""{schema}"".sp_yard_item       ADD COLUMN IF NOT EXISTS size_profile_id bigint;
            ALTER TABLE ""{schema}"".sp_yard_work_order ADD COLUMN IF NOT EXISTS rework_activity_id bigint;";

        /// <summary>Feature flags and tuning. Natural key: config is addressed by name everywhere.</summary>
        public const string CreateSystemConfig = @"
            CREATE TABLE IF NOT EXISTS ""{schema}"".sp_yard_system_config (
                config_key   varchar(64) NOT NULL,
                config_value text,
                created_by   bigint,
                created_on   timestamp NOT NULL DEFAULT NOW(),
                updated_by   bigint,
                updated_on   timestamp NOT NULL DEFAULT NOW(),
                CONSTRAINT pk_sp_yard_system_config PRIMARY KEY (config_key)
            );";

        // ── Seeds (INSERT ... WHERE NOT EXISTS, so re-provisioning never resets a live yard) ──

        public const string SeedMaterialType = @"
            INSERT INTO ""{schema}"".sp_yard_material_type
                (type_key, singular, plural, id_label, position_label, attribute_defs, rule_set_key,
                 created_by, created_on, updated_by, updated_on)
            SELECT @type_key, @singular, @plural, @id_label, @position_label, @attribute_defs::jsonb, @rule_set_key,
                   @user_id, NOW(), @user_id, NOW()
            WHERE NOT EXISTS (
                SELECT 1 FROM ""{schema}"".sp_yard_material_type
                WHERE LOWER(type_key) = LOWER(@type_key) AND is_deleted = FALSE
            );";

        public const string SeedConfig = @"
            INSERT INTO ""{schema}"".sp_yard_system_config
                (config_key, config_value, created_by, created_on, updated_by, updated_on)
            SELECT @config_key, @config_value, @user_id, NOW(), @user_id, NOW()
            WHERE NOT EXISTS (
                SELECT 1 FROM ""{schema}"".sp_yard_system_config WHERE config_key = @config_key
            );";

        /// <summary>Used by the schema context to tell "provisioned here" from "not enabled here".</summary>
        public const string ModuleTableExists = @"
            SELECT COUNT(*) FROM information_schema.tables
            WHERE table_schema = @schemaName AND table_name = 'sp_yard_zone';";

        /// <summary>
        /// Display name and description for the Masters UI. The platform keeps this as JSON in the
        /// table comment ({"n":name,"d":description}); the provisioner doubles the quotes.
        /// </summary>
        public const string CommentOnTable = @"COMMENT ON TABLE ""{schema}"".{table} IS '{comment}';";
    }
}
