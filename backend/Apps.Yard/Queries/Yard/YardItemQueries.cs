namespace Primworth_API.Queries.Yard
{
    /// <summary>SQL for items and for the placement rule's candidate list.</summary>
    public static class YardItemQueries
    {
        private const string Select = @"
            SELECT i.item_id, i.item_code, i.heat_number, i.material_type_id, i.grade_code, i.weight_kg,
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

        /// <summary>Items stacked above a given one in the same position, top first: the dig list.</summary>
        public const string GetItemsAbove = Select + @"
            WHERE i.position_id = @position_id AND i.is_deleted = FALSE AND i.stack_index > @stack_index
            ORDER BY i.stack_index DESC;";

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
                   s.top_grade, s.distinct_grades
            FROM ""{schema}"".sp_yard_position p
            JOIN ""{schema}"".sp_yard_zone z
              ON z.zone_id = p.zone_id AND z.is_active = TRUE AND z.is_deleted = FALSE
            JOIN ""{schema}"".sp_yard_zone_material_type zm
              ON zm.zone_id = z.zone_id AND zm.material_type_id = @material_type_id
            LEFT JOIN (
                SELECT position_id, COUNT(*) AS occupied,
                       (ARRAY_AGG(COALESCE(NULLIF(TRIM(grade_code), ''), 'Standard') ORDER BY stack_index DESC))[1] AS top_grade,
                       STRING_AGG(DISTINCT COALESCE(NULLIF(TRIM(grade_code), ''), 'Standard'), ',') AS distinct_grades
                FROM ""{schema}"".sp_yard_item
                WHERE is_deleted = FALSE AND position_id IS NOT NULL
                GROUP BY position_id
            ) s ON s.position_id = p.position_id
            LEFT JOIN (
                SELECT destination_position_id, COUNT(*) AS pending
                FROM ""{schema}"".sp_yard_work_order
                WHERE is_deleted = FALSE AND order_status IN ('PENDING', 'IN_PROGRESS')
                  AND destination_position_id IS NOT NULL
                GROUP BY destination_position_id
            ) o ON o.destination_position_id = p.position_id
            WHERE p.is_deleted = FALSE
              AND (@zone_purpose IS NULL OR z.zone_purpose = @zone_purpose)
            ORDER BY (p.capacity_limit - COALESCE(s.occupied, 0) - COALESCE(o.pending, 0)) DESC,
                     p.row_index, p.col_index;";
    }
}
