namespace Primworth_API.Queries.Yard
{
    /// <summary>SQL for items and for the placement rule's candidate list.</summary>
    public static class YardItemQueries
    {
        private const string Select = @"
            SELECT i.item_id, i.item_code, i.heat_number, i.material_type_id, i.grade_code, i.weight_kg,
                   CASE WHEN jsonb_typeof(i.attributes->'length_mm') = 'number' THEN (i.attributes->>'length_mm')::numeric
                        WHEN i.attributes->>'length_mm' ~ '^[0-9]+([.][0-9]+)?$' THEN (i.attributes->>'length_mm')::numeric END AS length_mm,
                   CASE WHEN jsonb_typeof(i.attributes->'width_mm') = 'number' THEN (i.attributes->>'width_mm')::numeric
                        WHEN i.attributes->>'width_mm' ~ '^[0-9]+([.][0-9]+)?$' THEN (i.attributes->>'width_mm')::numeric END AS width_mm,
                   CASE WHEN jsonb_typeof(i.attributes->'thickness_mm') = 'number' THEN (i.attributes->>'thickness_mm')::numeric
                        WHEN i.attributes->>'thickness_mm' ~ '^[0-9]+([.][0-9]+)?$' THEN (i.attributes->>'thickness_mm')::numeric END AS thickness_mm,
                   i.attributes::text AS attributes, i.item_status, i.quality_status,
                   i.zone_id, i.position_id, i.stack_index, i.arrived_on, i.is_virtual, i.size_profile_id, i.updated_on,
                   mt.type_key, z.zone_name, z.storage_style, p.row_index, p.col_index
            FROM ""{schema}"".sp_yard_item i
            JOIN ""{schema}"".sp_yard_material_type mt ON mt.material_type_id = i.material_type_id
            LEFT JOIN ""{schema}"".sp_yard_zone     z  ON z.zone_id = i.zone_id
            LEFT JOIN ""{schema}"".sp_yard_position p  ON p.position_id = i.position_id";

        public const string GetItems = Select + @"
            WHERE i.is_deleted = FALSE AND i.is_virtual = FALSE
              AND (@item_status IS NULL OR i.item_status = @item_status)
              AND (@quality_status IS NULL OR i.quality_status = @quality_status)
              AND (@zone_id IS NULL OR i.zone_id = @zone_id)
              AND (@search = '' OR LOWER(i.item_code) LIKE '%' || @search || '%'
                                OR LOWER(COALESCE(i.heat_number, '')) LIKE '%' || @search || '%')
            ORDER BY i.position_id NULLS FIRST, i.stack_index DESC, i.item_id
            LIMIT @page_size OFFSET @offset;";

        public const string GetItemById = Select + @"
            WHERE i.item_id = @item_id AND i.is_deleted = FALSE;";

        public const string GetItemByCode = Select + @"
            WHERE LOWER(i.item_code) = LOWER(@item_code) AND i.is_deleted = FALSE;";

        /// <summary>
        /// Highest trailing numbers already used for this item prefix (SL-4113) and for heats (H-40816).
        /// Zero when nothing matches, so the first generated code is PREFIX-0001 / H-00001.
        /// </summary>
        public const string GetNextInboundSerials = @"
            SELECT
                COALESCE((
                    SELECT MAX(CAST(substring(item_code from '[0-9]+$') AS int))
                    FROM ""{schema}"".sp_yard_item
                    WHERE is_deleted = FALSE
                      AND item_code ~ ('^' || @item_prefix || '-[0-9]+$')
                ), 0) AS last_item,
                COALESCE((
                    SELECT MAX(CAST(substring(heat_number from '[0-9]+$') AS int))
                    FROM ""{schema}"".sp_yard_item
                    WHERE is_deleted = FALSE
                      AND heat_number ~ '^H-[0-9]+$'
                ), 0) AS last_heat;";

        /// <summary>Items stacked above a given one in the same position, top first: the dig list.</summary>
        public const string GetItemsAbove = Select + @"
            WHERE i.position_id = @position_id AND i.is_deleted = FALSE AND i.stack_index > @stack_index
            ORDER BY i.stack_index DESC;";

        public const string GetSizeProfileById = @"
            SELECT size_profile_id, profile_name, material_type_id, length_mm, width_mm, thickness_mm, tolerance_pct, is_active
            FROM ""{schema}"".sp_yard_size_profile
            WHERE size_profile_id = @size_profile_id AND is_deleted = FALSE;";

        public const string InsertItem = @"
            INSERT INTO ""{schema}"".sp_yard_item
                (item_code, heat_number, material_type_id, grade_code, weight_kg, attributes, size_profile_id,
                 item_status, quality_status, created_by, created_on, updated_by, updated_on)
            VALUES
                (@item_code, @heat_number, @material_type_id, @grade_code, @weight_kg, @attributes::jsonb, @size_profile_id,
                 'PENDING', 'PENDING', @user_id, NOW(), @user_id, NOW())
            RETURNING item_id;";

        public const string UpdateItemStatus = @"
            UPDATE ""{schema}"".sp_yard_item
               SET item_status = @item_status, updated_by = @user_id, updated_on = NOW()
             WHERE item_id = @item_id AND is_deleted = FALSE;";

        public const string SetQualityStatus = @"
            UPDATE ""{schema}"".sp_yard_item
               SET quality_status = @quality_status, updated_by = @user_id, updated_on = NOW()
             WHERE item_id = @item_id AND is_deleted = FALSE;";

        /// <summary>
        /// Locks the destination position for the transaction. Placement reads MAX(stack_index) and
        /// writes MAX+1; without the lock two operators completing into one position could compute
        /// the same index and the unique index would reject one of them with an ugly error.
        /// FOR UPDATE OF p so the zone row is not locked too.
        /// </summary>
        public const string LockPosition = @"
            SELECT p.position_id, p.zone_id, p.row_index, p.col_index, p.capacity_limit,
                   z.zone_name, z.zone_purpose, z.storage_style, 0 AS occupied
            FROM ""{schema}"".sp_yard_position p
            JOIN ""{schema}"".sp_yard_zone z ON z.zone_id = p.zone_id
            WHERE p.position_id = @position_id AND p.is_deleted = FALSE
            FOR UPDATE OF p;";

        public const string NextStackIndex = @"
            SELECT COALESCE(MAX(stack_index), 0) + 1 FROM ""{schema}"".sp_yard_item
            WHERE position_id = @position_id AND is_deleted = FALSE;";

        public const string PlaceItem = @"
            UPDATE ""{schema}"".sp_yard_item
               SET zone_id = @zone_id, position_id = @position_id, stack_index = @stack_index,
                   item_status = @item_status, updated_by = @user_id, updated_on = NOW()
             WHERE item_id = @item_id AND is_deleted = FALSE;";

        /// <summary>Leaving the yard: clear the location. The caller repacks what was above.</summary>
        public const string ClearItemLocation = @"
            UPDATE ""{schema}"".sp_yard_item
               SET zone_id = NULL, position_id = NULL, stack_index = NULL,
                   item_status = @item_status, updated_by = @user_id, updated_on = NOW()
             WHERE item_id = @item_id AND is_deleted = FALSE;";

        /// <summary>Closes the gap an item leaves in a stack so indices stay 1..N.</summary>
        public const string RepackAbove = @"
            UPDATE ""{schema}"".sp_yard_item
               SET stack_index = stack_index - 1, updated_by = @user_id, updated_on = NOW()
             WHERE position_id = @position_id AND is_deleted = FALSE AND stack_index > @removed_stack_index;";

        /// <summary>
        /// Positions the placement rule may use for an item of a given type, most free first.
        /// Occupancy counts items physically present PLUS open orders heading there, so two orders
        /// are never planned into the same last slot. The grade aggregate serves grade separation
        /// without a query per position.
        /// </summary>
        public const string GetCandidatePositions = @"
            SELECT p.position_id, p.zone_id, z.zone_name, z.zone_purpose, z.storage_style,
                   p.row_index, p.col_index, p.capacity_limit,
                   COALESCE(s.occupied, 0) + COALESCE(o.pending, 0) AS occupied,
                   COALESCE(q.grade_code, s.top_grade) AS top_grade,
                   s.distinct_grades,
                   COALESCE(q.size_profile_id, s.top_size_profile_id) AS top_size_profile_id,
                   COALESCE(q.length_mm, s.top_length_mm) AS top_length_mm,
                   COALESCE(q.width_mm, s.top_width_mm) AS top_width_mm,
                   COALESCE(q.thickness_mm, s.top_thickness_mm) AS top_thickness_mm
            FROM ""{schema}"".sp_yard_position p
            JOIN ""{schema}"".sp_yard_zone z
              ON z.zone_id = p.zone_id AND z.is_active = TRUE AND z.is_deleted = FALSE
            JOIN ""{schema}"".sp_yard_zone_material_type zm
              ON zm.zone_id = z.zone_id AND zm.material_type_id = @material_type_id
            LEFT JOIN (
                SELECT position_id, COUNT(*) AS occupied,
                       (ARRAY_AGG(COALESCE(NULLIF(TRIM(grade_code), ''), 'Standard') ORDER BY stack_index DESC))[1] AS top_grade,
                       (ARRAY_AGG(size_profile_id ORDER BY stack_index DESC))[1] AS top_size_profile_id,
                       (ARRAY_AGG(CASE WHEN jsonb_typeof(attributes->'length_mm') = 'number' THEN (attributes->>'length_mm')::numeric
                                       WHEN attributes->>'length_mm' ~ '^[0-9]+([.][0-9]+)?$' THEN (attributes->>'length_mm')::numeric END ORDER BY stack_index DESC))[1] AS top_length_mm,
                       (ARRAY_AGG(CASE WHEN jsonb_typeof(attributes->'width_mm') = 'number' THEN (attributes->>'width_mm')::numeric
                                       WHEN attributes->>'width_mm' ~ '^[0-9]+([.][0-9]+)?$' THEN (attributes->>'width_mm')::numeric END ORDER BY stack_index DESC))[1] AS top_width_mm,
                       (ARRAY_AGG(CASE WHEN jsonb_typeof(attributes->'thickness_mm') = 'number' THEN (attributes->>'thickness_mm')::numeric
                                       WHEN attributes->>'thickness_mm' ~ '^[0-9]+([.][0-9]+)?$' THEN (attributes->>'thickness_mm')::numeric END ORDER BY stack_index DESC))[1] AS top_thickness_mm,
                       STRING_AGG(DISTINCT COALESCE(NULLIF(TRIM(grade_code), ''), 'Standard'), ',') AS distinct_grades
                FROM ""{schema}"".sp_yard_item
                WHERE is_deleted = FALSE AND position_id IS NOT NULL
                GROUP BY position_id
            ) s ON s.position_id = p.position_id
            LEFT JOIN (
                SELECT w.destination_position_id, COUNT(*) AS pending
                FROM ""{schema}"".sp_yard_work_order w
                JOIN ""{schema}"".sp_yard_item i ON i.item_id = w.item_id
                WHERE w.is_deleted = FALSE AND w.order_status IN ('PENDING', 'IN_PROGRESS')
                  AND w.destination_position_id IS NOT NULL
                  AND (i.position_id IS DISTINCT FROM w.destination_position_id)
                GROUP BY w.destination_position_id
            ) o ON o.destination_position_id = p.position_id
            LEFT JOIN LATERAL (
                SELECT i.grade_code, i.size_profile_id,
                       CASE WHEN jsonb_typeof(i.attributes->'length_mm') = 'number' THEN (i.attributes->>'length_mm')::numeric
                            WHEN i.attributes->>'length_mm' ~ '^[0-9]+([.][0-9]+)?$' THEN (i.attributes->>'length_mm')::numeric END AS length_mm,
                       CASE WHEN jsonb_typeof(i.attributes->'width_mm') = 'number' THEN (i.attributes->>'width_mm')::numeric
                            WHEN i.attributes->>'width_mm' ~ '^[0-9]+([.][0-9]+)?$' THEN (i.attributes->>'width_mm')::numeric END AS width_mm,
                       CASE WHEN jsonb_typeof(i.attributes->'thickness_mm') = 'number' THEN (i.attributes->>'thickness_mm')::numeric
                            WHEN i.attributes->>'thickness_mm' ~ '^[0-9]+([.][0-9]+)?$' THEN (i.attributes->>'thickness_mm')::numeric END AS thickness_mm
                FROM ""{schema}"".sp_yard_work_order w
                JOIN ""{schema}"".sp_yard_item i ON i.item_id = w.item_id
                WHERE w.is_deleted = FALSE AND w.order_status IN ('PENDING', 'IN_PROGRESS')
                  AND w.destination_position_id = p.position_id
                ORDER BY w.work_order_id DESC
                LIMIT 1
            ) q ON TRUE
            WHERE p.is_deleted = FALSE
              AND (@zone_purpose IS NULL OR z.zone_purpose = @zone_purpose)
              AND (@yard_type_id IS NULL OR z.yard_type_id = @yard_type_id)
            ORDER BY (p.capacity_limit - COALESCE(s.occupied, 0) - COALESCE(o.pending, 0)) DESC,
                     p.row_index, p.col_index;";
    }
}
