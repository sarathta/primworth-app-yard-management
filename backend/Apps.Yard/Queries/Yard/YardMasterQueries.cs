namespace Primworth_API.Queries.Yard
{
    /// <summary>SQL for the masters the Configurator edits, the grade list, and the activity log.</summary>
    public static class YardMasterQueries
    {
        // ── Grades: the platform's own master, cloned into the facility schema ──
        public const string GradeTableExists = @"
            SELECT COUNT(*) FROM information_schema.tables
            WHERE table_schema = @schemaName AND table_name = 'gm_grade';";

        public const string GetGrades = @"
            SELECT id, grade_code, description FROM ""{schema}"".gm_grade ORDER BY grade_code;";

        // ── Size profiles ────────────────────────────────────────────────────────
        public const string GetSizeProfiles = @"
            SELECT size_profile_id, profile_name, material_type_id, length_mm, width_mm, thickness_mm, tolerance_pct, is_active
            FROM ""{schema}"".sp_yard_size_profile WHERE is_deleted = FALSE ORDER BY profile_name;";

        public const string InsertSizeProfile = @"
            INSERT INTO ""{schema}"".sp_yard_size_profile
                (profile_name, length_mm, width_mm, thickness_mm, tolerance_pct, is_active, created_by, created_on, updated_by, updated_on)
            VALUES (@profile_name, @length_mm, @width_mm, @thickness_mm, @tolerance_pct, @is_active, @user_id, NOW(), @user_id, NOW())
            RETURNING size_profile_id;";

        public const string UpdateSizeProfile = @"
            UPDATE ""{schema}"".sp_yard_size_profile
               SET profile_name = @profile_name, length_mm = @length_mm, width_mm = @width_mm, thickness_mm = @thickness_mm,
                   tolerance_pct = @tolerance_pct, is_active = @is_active, updated_by = @user_id, updated_on = NOW()
             WHERE size_profile_id = @id AND is_deleted = FALSE;";

        /// <summary>A profile cannot go while items or mixes still point at it.</summary>
        public const string CountSizeProfileUses = @"
            SELECT (SELECT COUNT(*) FROM ""{schema}"".sp_yard_item WHERE size_profile_id = @id AND is_deleted = FALSE)
                 + (SELECT COUNT(*) FROM ""{schema}"".sp_yard_product_mix_size WHERE size_profile_id = @id);";

        public const string SoftDeleteSizeProfile = @"
            UPDATE ""{schema}"".sp_yard_size_profile
               SET is_deleted = TRUE, is_active = FALSE, updated_by = @user_id, updated_on = NOW()
             WHERE size_profile_id = @id AND is_deleted = FALSE;";

        // ── Rework activities ────────────────────────────────────────────────────
        public const string GetReworkActivities = @"
            SELECT rework_activity_id, activity_name, description, is_active
            FROM ""{schema}"".sp_yard_rework_activity WHERE is_deleted = FALSE ORDER BY activity_name;";

        public const string GetReworkActivityById = @"
            SELECT rework_activity_id, activity_name, description, is_active
            FROM ""{schema}"".sp_yard_rework_activity WHERE rework_activity_id = @id AND is_deleted = FALSE;";

        public const string InsertReworkActivity = @"
            INSERT INTO ""{schema}"".sp_yard_rework_activity
                (activity_name, description, is_active, created_by, created_on, updated_by, updated_on)
            VALUES (@activity_name, @description, @is_active, @user_id, NOW(), @user_id, NOW())
            RETURNING rework_activity_id;";

        public const string UpdateReworkActivity = @"
            UPDATE ""{schema}"".sp_yard_rework_activity
               SET activity_name = @activity_name, description = @description, is_active = @is_active,
                   updated_by = @user_id, updated_on = NOW()
             WHERE rework_activity_id = @id AND is_deleted = FALSE;";

        /// <summary>Soft: the work orders that carried the activity out still say what was done.</summary>
        public const string SoftDeleteReworkActivity = @"
            UPDATE ""{schema}"".sp_yard_rework_activity
               SET is_deleted = TRUE, is_active = FALSE, updated_by = @user_id, updated_on = NOW()
             WHERE rework_activity_id = @id AND is_deleted = FALSE;";

        // ── Product mix: children aggregated inline, never fetched per row ───────
        private const string SelectMix = @"
            SELECT m.product_mix_id, m.mix_name, m.description, m.material_type_id, m.target_zone_purpose, m.priority, m.is_active,
                   COALESCE(g.grades, '') AS allowed_grades_str,
                   COALESCE(s.size_ids, '') AS allowed_size_ids_str,
                   COALESCE(s.size_names, '') AS allowed_size_names_str
            FROM ""{schema}"".sp_yard_product_mix m
            LEFT JOIN (
                SELECT product_mix_id, STRING_AGG(grade_code, ',' ORDER BY grade_code) AS grades
                FROM ""{schema}"".sp_yard_product_mix_grade GROUP BY product_mix_id
            ) g ON g.product_mix_id = m.product_mix_id
            LEFT JOIN (
                SELECT ps.product_mix_id,
                       STRING_AGG(ps.size_profile_id::text, ',' ORDER BY ps.size_profile_id) AS size_ids,
                       STRING_AGG(sp.profile_name, ',' ORDER BY ps.size_profile_id) AS size_names
                FROM ""{schema}"".sp_yard_product_mix_size ps
                LEFT JOIN ""{schema}"".sp_yard_size_profile sp ON sp.size_profile_id = ps.size_profile_id
                GROUP BY ps.product_mix_id
            ) s ON s.product_mix_id = m.product_mix_id";

        public const string GetProductMixes = SelectMix + @"
            WHERE m.is_deleted = FALSE ORDER BY m.priority DESC, m.mix_name;";

        /// <summary>Active mixes in matching order, highest priority first. An empty child set means "any".</summary>
        public const string GetActiveMixesForRouting = SelectMix + @"
            WHERE m.is_deleted = FALSE AND m.is_active = TRUE ORDER BY m.priority DESC, m.product_mix_id;";

        public const string CountMixByName = @"
            SELECT COUNT(*) FROM ""{schema}"".sp_yard_product_mix
            WHERE is_deleted = FALSE AND LOWER(TRIM(mix_name)) = LOWER(TRIM(@mix_name)) AND (@product_mix_id = 0 OR product_mix_id <> @product_mix_id);";

        public const string InsertProductMix = @"
            INSERT INTO ""{schema}"".sp_yard_product_mix
                (mix_name, description, material_type_id, target_zone_purpose, priority, is_active, created_by, created_on, updated_by, updated_on)
            VALUES (@mix_name, @description, @material_type_id, @target_zone_purpose, @priority, @is_active, @user_id, NOW(), @user_id, NOW())
            RETURNING product_mix_id;";

        public const string UpdateProductMix = @"
            UPDATE ""{schema}"".sp_yard_product_mix
               SET mix_name = @mix_name, description = @description, material_type_id = @material_type_id,
                   target_zone_purpose = @target_zone_purpose, priority = @priority, is_active = @is_active,
                   updated_by = @user_id, updated_on = NOW()
             WHERE product_mix_id = @product_mix_id AND is_deleted = FALSE;";

        // Children are replaced wholesale inside the parent's transaction.
        public const string DeleteMixGrades = @"DELETE FROM ""{schema}"".sp_yard_product_mix_grade WHERE product_mix_id = @product_mix_id;";
        public const string InsertMixGrade = @"INSERT INTO ""{schema}"".sp_yard_product_mix_grade (product_mix_id, grade_code) VALUES (@product_mix_id, @grade_code);";
        public const string DeleteMixSizes = @"DELETE FROM ""{schema}"".sp_yard_product_mix_size WHERE product_mix_id = @product_mix_id;";
        public const string InsertMixSize = @"INSERT INTO ""{schema}"".sp_yard_product_mix_size (product_mix_id, size_profile_id) VALUES (@product_mix_id, @size_profile_id);";

        public const string SoftDeleteProductMix = @"
            UPDATE ""{schema}"".sp_yard_product_mix
               SET is_deleted = TRUE, updated_by = @user_id, updated_on = NOW()
             WHERE product_mix_id = @product_mix_id AND is_deleted = FALSE;";

        // ── Activity log ─────────────────────────────────────────────────────────
        public const string InsertSystemLog = @"
            INSERT INTO ""{schema}"".sp_yard_system_log (user_id, action, details, log_timestamp)
            VALUES (@user_id, @action, @details::jsonb, NOW());";

        // Every parameter is cast: Postgres cannot infer the type of a parameter that only appears
        // beside IS NULL. `to` is exclusive of the following day so one date in both boxes is a whole day.
        private const string LogFilter = @"
            WHERE (@action::text IS NULL OR action ILIKE '%' || @action::text || '%')
              AND (@from::timestamp IS NULL OR log_timestamp >= @from::timestamp)
              AND (@to::timestamp IS NULL OR log_timestamp < (@to::timestamp::date + 1))";

        public const string GetSystemLogs = @"
            SELECT system_log_id AS id, user_id, action, details::text AS details, log_timestamp AS timestamp
            FROM ""{schema}"".sp_yard_system_log " + LogFilter + @"
            ORDER BY log_timestamp DESC LIMIT @limit OFFSET @offset;";

        public const string CountSystemLogs = @"SELECT COUNT(*) FROM ""{schema}"".sp_yard_system_log " + LogFilter + ";";
    }
}
