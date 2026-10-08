namespace Primworth_API.Queries.Yard
{
    /// <summary>SQL for areas, positions and material types. "{schema}" is the only interpolation.</summary>
    public static class YardZoneQueries
    {
        public const string GetMaterialTypes = @"
            SELECT material_type_id, type_key, singular, plural, id_label, position_label,
                   attribute_defs::text AS attribute_defs, rule_set_key, is_active
            FROM ""{schema}"".sp_yard_material_type
            WHERE is_deleted = FALSE AND (@include_inactive OR is_active)
            ORDER BY type_key;";

        public const string GetMaterialTypeByKey = @"
            SELECT material_type_id, type_key, singular, plural, id_label, position_label,
                   attribute_defs::text AS attribute_defs, rule_set_key, is_active
            FROM ""{schema}"".sp_yard_material_type
            WHERE LOWER(type_key) = LOWER(@type_key) AND is_deleted = FALSE;";

        /// <summary>The facility's one active type, when there is exactly one. Lets a slab plant omit the key.</summary>
        public const string GetOnlyActiveMaterialType = @"
            SELECT material_type_id, type_key, singular, plural, id_label, position_label,
                   attribute_defs::text AS attribute_defs, rule_set_key, is_active
            FROM ""{schema}"".sp_yard_material_type
            WHERE is_deleted = FALSE AND is_active
            LIMIT 2;";

        public const string GetZones = @"
            SELECT z.zone_id, z.zone_name, z.zone_purpose, z.storage_style, z.is_active,
                   COALESCE(p.position_count, 0) AS position_count,
                   COALESCE(p.total_capacity, 0) AS total_capacity,
                   COALESCE(i.occupied, 0)       AS occupied,
                   m.accepted_types
            FROM ""{schema}"".sp_yard_zone z
            LEFT JOIN (
                SELECT zone_id, COUNT(*) AS position_count, SUM(capacity_limit) AS total_capacity
                FROM ""{schema}"".sp_yard_position WHERE is_deleted = FALSE GROUP BY zone_id
            ) p ON p.zone_id = z.zone_id
            LEFT JOIN (
                SELECT zone_id, COUNT(*) AS occupied
                FROM ""{schema}"".sp_yard_item
                WHERE is_deleted = FALSE AND is_virtual = FALSE AND position_id IS NOT NULL
                GROUP BY zone_id
            ) i ON i.zone_id = z.zone_id
            LEFT JOIN (
                SELECT zm.zone_id, STRING_AGG(mt.type_key, ',' ORDER BY mt.type_key) AS accepted_types
                FROM ""{schema}"".sp_yard_zone_material_type zm
                JOIN ""{schema}"".sp_yard_material_type mt ON mt.material_type_id = zm.material_type_id
                GROUP BY zm.zone_id
            ) m ON m.zone_id = z.zone_id
            WHERE z.is_deleted = FALSE
            ORDER BY CASE z.zone_purpose WHEN 'STORAGE' THEN 0 WHEN 'REWORK' THEN 1 WHEN 'DISPATCH' THEN 2 ELSE 3 END,
                     z.zone_name;";

        public const string GetPositionsByZone = @"
            SELECT p.position_id, p.zone_id, p.row_index, p.col_index, p.capacity_limit,
                   COALESCE(i.occupied, 0) AS occupied
            FROM ""{schema}"".sp_yard_position p
            LEFT JOIN (
                SELECT position_id, COUNT(*) AS occupied
                FROM ""{schema}"".sp_yard_item
                WHERE is_deleted = FALSE AND position_id IS NOT NULL
                GROUP BY position_id
            ) i ON i.position_id = p.position_id
            WHERE p.zone_id = @zone_id AND p.is_deleted = FALSE
            ORDER BY p.row_index, p.col_index;";

        public const string CountActiveByName = @"
            SELECT COUNT(*) FROM ""{schema}"".sp_yard_zone
            WHERE is_deleted = FALSE AND LOWER(TRIM(zone_name)) = LOWER(TRIM(@zone_name));";

        public const string InsertZone = @"
            INSERT INTO ""{schema}"".sp_yard_zone
                (zone_name, zone_purpose, storage_style, created_by, created_on, updated_by, updated_on)
            VALUES (@zone_name, @zone_purpose, @storage_style, @user_id, NOW(), @user_id, NOW())
            RETURNING zone_id;";

        public const string InsertZoneMaterialType = @"
            INSERT INTO ""{schema}"".sp_yard_zone_material_type (zone_id, material_type_id)
            SELECT @zone_id, material_type_id
            FROM ""{schema}"".sp_yard_material_type
            WHERE LOWER(type_key) = LOWER(@type_key) AND is_deleted = FALSE
            ON CONFLICT DO NOTHING;";

        /// <summary>The whole rows x cols grid in one statement.</summary>
        public const string InsertPositionGrid = @"
            INSERT INTO ""{schema}"".sp_yard_position
                (zone_id, row_index, col_index, capacity_limit, created_by, created_on, updated_by, updated_on)
            SELECT @zone_id, r, c, @capacity_limit, @user_id, NOW(), @user_id, NOW()
            FROM generate_series(1, @rows) r, generate_series(1, @cols) c;";

        public const string CountItemsInPosition = @"
            SELECT COUNT(*) FROM ""{schema}"".sp_yard_item
            WHERE position_id = @position_id AND is_deleted = FALSE;";

        public const string CountItemsInZone = @"
            SELECT COUNT(*) FROM ""{schema}"".sp_yard_item
            WHERE zone_id = @zone_id AND is_deleted = FALSE;";

        public const string UpdatePositionCapacity = @"
            UPDATE ""{schema}"".sp_yard_position
               SET capacity_limit = @capacity_limit, updated_by = @user_id, updated_on = NOW()
             WHERE position_id = @position_id AND is_deleted = FALSE;";

        public const string SoftDeleteZone = @"
            UPDATE ""{schema}"".sp_yard_position SET is_deleted = TRUE, updated_by = @user_id, updated_on = NOW()
             WHERE zone_id = @zone_id;
            UPDATE ""{schema}"".sp_yard_zone SET is_deleted = TRUE, updated_by = @user_id, updated_on = NOW()
             WHERE zone_id = @zone_id AND is_deleted = FALSE;";

        public const string GetFirstActiveZoneOfPurpose = @"
            SELECT zone_id FROM ""{schema}"".sp_yard_zone
             WHERE zone_purpose = @zone_purpose AND is_active = TRUE AND is_deleted = FALSE
             ORDER BY zone_id LIMIT 1;";

        public const string GetConfig = @"
            SELECT config_key, config_value FROM ""{schema}"".sp_yard_system_config ORDER BY config_key;";

        public const string UpsertConfig = @"
            INSERT INTO ""{schema}"".sp_yard_system_config (config_key, config_value, created_by, created_on, updated_by, updated_on)
            VALUES (@config_key, @config_value, @user_id, NOW(), @user_id, NOW())
            ON CONFLICT (config_key) DO UPDATE SET config_value = EXCLUDED.config_value, updated_by = EXCLUDED.updated_by, updated_on = NOW();";
    }
}
