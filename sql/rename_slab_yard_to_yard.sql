-- One-time rename of a live Slab Yard (mSYMS) facility to the generic Yard Management shape.
--
-- Why a script and not the provisioner: the provisioner only ever creates, so a rename is the one
-- change it cannot make. Run this ONCE per facility schema that has sp_syms_* tables, inside a
-- transaction, before the first start of the build that ships YardProvisioner. The provisioner
-- then finds every sp_yard_* table already present and adds only what is new (material types,
-- storage style, attributes), through its normal IF NOT EXISTS path.
--
-- Usage:  sed 's/{schema}/1_plant_3_yard_db/g' rename_slab_yard_to_yard.sql | psql -d primworth
--
-- Everything below is guarded: a table or column that has already been renamed is skipped, so
-- running it twice changes nothing. Indexes and constraints keep their old names; Postgres does
-- not care, and renaming them buys nothing.

BEGIN;

DO $$
DECLARE s text := '{schema}';
BEGIN
  -- ── Tables ──────────────────────────────────────────────────────────────────
  IF to_regclass(format('%I.sp_syms_zone', s)) IS NOT NULL AND to_regclass(format('%I.sp_yard_zone', s)) IS NULL THEN
    EXECUTE format('ALTER TABLE %I.sp_syms_zone RENAME TO sp_yard_zone', s);
  END IF;
  IF to_regclass(format('%I.sp_syms_column', s)) IS NOT NULL AND to_regclass(format('%I.sp_yard_position', s)) IS NULL THEN
    EXECUTE format('ALTER TABLE %I.sp_syms_column RENAME TO sp_yard_position', s);
    EXECUTE format('ALTER TABLE %I.sp_yard_position RENAME COLUMN column_id TO position_id', s);
  END IF;
  IF to_regclass(format('%I.sp_syms_slab', s)) IS NOT NULL AND to_regclass(format('%I.sp_yard_item', s)) IS NULL THEN
    EXECUTE format('ALTER TABLE %I.sp_syms_slab RENAME TO sp_yard_item', s);
    EXECUTE format('ALTER TABLE %I.sp_yard_item RENAME COLUMN slab_id TO item_id', s);
    EXECUTE format('ALTER TABLE %I.sp_yard_item RENAME COLUMN slab_code TO item_code', s);
    EXECUTE format('ALTER TABLE %I.sp_yard_item RENAME COLUMN slab_status TO item_status', s);
    EXECUTE format('ALTER TABLE %I.sp_yard_item RENAME COLUMN column_id TO position_id', s);
  END IF;
  IF to_regclass(format('%I.sp_syms_work_order', s)) IS NOT NULL AND to_regclass(format('%I.sp_yard_work_order', s)) IS NULL THEN
    EXECUTE format('ALTER TABLE %I.sp_syms_work_order RENAME TO sp_yard_work_order', s);
    EXECUTE format('ALTER TABLE %I.sp_yard_work_order RENAME COLUMN slab_id TO item_id', s);
    EXECUTE format('ALTER TABLE %I.sp_yard_work_order RENAME COLUMN source_column_id TO source_position_id', s);
    EXECUTE format('ALTER TABLE %I.sp_yard_work_order RENAME COLUMN destination_column_id TO destination_position_id', s);
  END IF;
  IF to_regclass(format('%I.sp_syms_slab_movement_log', s)) IS NOT NULL AND to_regclass(format('%I.sp_yard_movement_log', s)) IS NULL THEN
    EXECUTE format('ALTER TABLE %I.sp_syms_slab_movement_log RENAME TO sp_yard_movement_log', s);
    EXECUTE format('ALTER TABLE %I.sp_yard_movement_log RENAME COLUMN slab_id TO item_id', s);
    EXECUTE format('ALTER TABLE %I.sp_yard_movement_log RENAME COLUMN from_col_desc TO from_position', s);
    EXECUTE format('ALTER TABLE %I.sp_yard_movement_log RENAME COLUMN to_col_desc TO to_position', s);
  END IF;
  IF to_regclass(format('%I.sp_syms_system_config', s)) IS NOT NULL AND to_regclass(format('%I.sp_yard_system_config', s)) IS NULL THEN
    EXECUTE format('ALTER TABLE %I.sp_syms_system_config RENAME TO sp_yard_system_config', s);
  END IF;
  -- Tables the generic module keeps under the same shape, renamed for the prefix only.
  IF to_regclass(format('%I.sp_syms_system_log', s)) IS NOT NULL AND to_regclass(format('%I.sp_yard_system_log', s)) IS NULL THEN
    EXECUTE format('ALTER TABLE %I.sp_syms_system_log RENAME TO sp_yard_system_log', s);
  END IF;
  IF to_regclass(format('%I.sp_syms_integration_endpoint', s)) IS NOT NULL AND to_regclass(format('%I.sp_yard_integration_endpoint', s)) IS NULL THEN
    EXECUTE format('ALTER TABLE %I.sp_syms_integration_endpoint RENAME TO sp_yard_integration_endpoint', s);
  END IF;
  IF to_regclass(format('%I.sp_syms_decision_model', s)) IS NOT NULL AND to_regclass(format('%I.sp_yard_decision_model', s)) IS NULL THEN
    EXECUTE format('ALTER TABLE %I.sp_syms_decision_model RENAME TO sp_yard_decision_model', s);
  END IF;
  IF to_regclass(format('%I.sp_syms_compromised_zone', s)) IS NOT NULL AND to_regclass(format('%I.sp_yard_compromised_zone', s)) IS NULL THEN
    EXECUTE format('ALTER TABLE %I.sp_syms_compromised_zone RENAME TO sp_yard_compromised_zone', s);
  END IF;
  IF to_regclass(format('%I.sp_syms_dimension_profile', s)) IS NOT NULL AND to_regclass(format('%I.sp_yard_size_profile', s)) IS NULL THEN
    EXECUTE format('ALTER TABLE %I.sp_syms_dimension_profile RENAME TO sp_yard_size_profile', s);
  END IF;
  IF to_regclass(format('%I.sp_syms_rework_activity', s)) IS NOT NULL AND to_regclass(format('%I.sp_yard_rework_activity', s)) IS NULL THEN
    EXECUTE format('ALTER TABLE %I.sp_syms_rework_activity RENAME TO sp_yard_rework_activity', s);
  END IF;
  IF to_regclass(format('%I.sp_syms_product_mix', s)) IS NOT NULL AND to_regclass(format('%I.sp_yard_product_mix', s)) IS NULL THEN
    EXECUTE format('ALTER TABLE %I.sp_syms_product_mix RENAME TO sp_yard_product_mix', s);
    EXECUTE format('ALTER TABLE %I.sp_syms_product_mix_grade RENAME TO sp_yard_product_mix_grade', s);
    EXECUTE format('ALTER TABLE %I.sp_syms_product_mix_dimension RENAME TO sp_yard_product_mix_size', s);
  END IF;

  -- ── Columns the generic item needs ───────────────────────────────────────────
  EXECUTE format('ALTER TABLE %I.sp_yard_zone ADD COLUMN IF NOT EXISTS storage_style varchar(10) NOT NULL DEFAULT ''STACK''', s);
  EXECUTE format('ALTER TABLE %I.sp_yard_zone ADD COLUMN IF NOT EXISTS zone_purpose varchar(20)', s);
  EXECUTE format('UPDATE %I.sp_yard_zone SET zone_purpose = CASE zone_type WHEN ''SCARFING'' THEN ''REWORK'' ELSE zone_type END WHERE zone_purpose IS NULL', s);
  EXECUTE format('ALTER TABLE %I.sp_yard_item ADD COLUMN IF NOT EXISTS material_type_id bigint', s);
  EXECUTE format('ALTER TABLE %I.sp_yard_item ADD COLUMN IF NOT EXISTS attributes jsonb NOT NULL DEFAULT ''{}''::jsonb', s);
  EXECUTE format('ALTER TABLE %I.sp_yard_item ADD COLUMN IF NOT EXISTS arrived_on timestamp NOT NULL DEFAULT NOW()', s);
END $$;

-- ── Material type SLAB, and every existing item becomes one ───────────────────
-- (The provisioner would seed this row too; doing it here lets the data copy below run now.)
INSERT INTO "{schema}".sp_yard_material_type (type_key, singular, plural, id_label, position_label, attribute_defs, rule_set_key)
SELECT 'SLAB', 'Slab', 'Slabs', 'Slab ID', 'Pit R{row}-C{col}',
       '[{"key":"length_mm","label":"Length","type":"number","unit":"mm","required":false},
         {"key":"width_mm","label":"Width","type":"number","unit":"mm","required":false},
         {"key":"thickness_mm","label":"Thickness","type":"number","unit":"mm","required":false},
         {"key":"cast_time","label":"Cast time","type":"datetime","required":false},
         {"key":"rolling_width_mm","label":"Rolling width","type":"number","unit":"mm","required":false},
         {"key":"target_reheat_c","label":"Reheat target","type":"integer","unit":"C","required":false}]'::jsonb,
       'slab-routing'
WHERE to_regclass('"{schema}".sp_yard_material_type') IS NOT NULL
  AND NOT EXISTS (SELECT 1 FROM "{schema}".sp_yard_material_type WHERE type_key = 'SLAB');

UPDATE "{schema}".sp_yard_item i
   SET material_type_id = (SELECT material_type_id FROM "{schema}".sp_yard_material_type WHERE type_key = 'SLAB')
 WHERE i.material_type_id IS NULL;

-- Slab-only columns move into the attribute bag. Both dimension spellings ({l,w,t} and
-- {length,width,thickness}) are read. The old columns stay until no screen reads them.
UPDATE "{schema}".sp_yard_item i
   SET attributes = jsonb_strip_nulls(jsonb_build_object(
         'length_mm',        COALESCE((i.dimensions->>'length')::numeric, (i.dimensions->>'l')::numeric),
         'width_mm',         COALESCE((i.dimensions->>'width')::numeric,  (i.dimensions->>'w')::numeric),
         'thickness_mm',     COALESCE((i.dimensions->>'thickness')::numeric, (i.dimensions->>'t')::numeric),
         'cast_time',        i.cast_time,
         'rolling_width_mm', i.rolling_width,
         'target_reheat_c',  i.target_reheat_temp))
 WHERE i.attributes = '{}'::jsonb;

-- Every existing area is a stack that accepts slabs.
INSERT INTO "{schema}".sp_yard_zone_material_type (zone_id, material_type_id)
SELECT z.zone_id, mt.material_type_id
  FROM "{schema}".sp_yard_zone z
  CROSS JOIN (SELECT material_type_id FROM "{schema}".sp_yard_material_type WHERE type_key = 'SLAB') mt
 WHERE to_regclass('"{schema}".sp_yard_zone_material_type') IS NOT NULL
   AND NOT EXISTS (SELECT 1 FROM "{schema}".sp_yard_zone_material_type x WHERE x.zone_id = z.zone_id);

COMMIT;

-- Afterwards, in public: the application row and the permission names.
-- UPDATE public.application
--    SET application_name = 'Yard Management', app_key = 'yard_management', route_path = '/yard',
--        module_table_prefix = 'sp_yard_', provisioner_key = 'yard_management', permission_code = 'YRDL'
--  WHERE app_key = 'slab_yard';
-- Permission codes are data in every site's role mappings; rename their display names, keep the
-- codes, or map SYMS->YRDL, SYMD->YRDD, SYMO->YRDO, SYMA->YRDA in sys_role_permissions on the one
-- test facility while that is still cheap.
