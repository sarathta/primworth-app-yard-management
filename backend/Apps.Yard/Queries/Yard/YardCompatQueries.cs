namespace Primworth_API.Queries.Yard
{
    /// <summary>
    /// Queries whose result shape is dictated by the embedded mSYMS screens rather than by us.
    ///
    /// The screens were written against Prisma and read nested objects: a work order carries its
    /// slab, source zone and destination column as objects. These queries stay flat; the compat
    /// controllers nest them. Hot/warm/cold is a clock, not a stored state: it is computed from
    /// the item's cast_time attribute against the two thermal config values at read time.
    /// </summary>
    public static class YardCompatQueries
    {
        /// <summary>HOT within @hot minutes of casting, WARM within @warm, otherwise COLD. No cast time: COLD.</summary>
        public const string Temperature = @"
            CASE WHEN NULLIF(i.attributes->>'cast_time', '') IS NULL THEN 'COLD'
                 WHEN NOW() - (i.attributes->>'cast_time')::timestamp < make_interval(mins => @hot)  THEN 'HOT'
                 WHEN NOW() - (i.attributes->>'cast_time')::timestamp < make_interval(mins => @warm) THEN 'WARM'
                 ELSE 'COLD' END";

        // ── Yard overview: three flat queries, assembled in memory ───────────────
        public const string OverviewZones = @"
            SELECT zone_id, zone_name, zone_purpose, storage_style, is_active
            FROM ""{schema}"".sp_yard_zone WHERE is_deleted = FALSE
            ORDER BY CASE zone_purpose WHEN 'STORAGE' THEN 0 WHEN 'REWORK' THEN 1 WHEN 'DISPATCH' THEN 2 ELSE 3 END, zone_name;";

        public const string OverviewPositions = @"
            SELECT position_id, zone_id, row_index, col_index, capacity_limit
            FROM ""{schema}"".sp_yard_position WHERE is_deleted = FALSE ORDER BY row_index, col_index;";

        public const string OverviewItems = @"
            SELECT i.item_id, i.position_id, i.item_code, i.heat_number, i.stack_index, i.item_status, i.quality_status,
                   i.grade_code, " + Temperature + @" AS temperature_state
            FROM ""{schema}"".sp_yard_item i
            WHERE i.is_deleted = FALSE AND i.position_id IS NOT NULL AND i.is_virtual = FALSE
            ORDER BY i.position_id, i.stack_index;";

        // ── Items in the full compat shape ───────────────────────────────────────
        private const string SelectItem = @"
            SELECT i.item_id, i.item_code, i.heat_number, i.material_type_id, i.grade_code, i.weight_kg,
                   i.attributes::text AS attributes, i.item_status, i.quality_status,
                   i.zone_id, i.position_id, i.stack_index, i.arrived_on, i.is_virtual, i.size_profile_id, i.updated_on,
                   mt.type_key, z.zone_name, z.storage_style, p.row_index, p.col_index,
                   z.zone_purpose, " + Temperature + @" AS temperature_state
            FROM ""{schema}"".sp_yard_item i
            JOIN ""{schema}"".sp_yard_material_type mt ON mt.material_type_id = i.material_type_id
            LEFT JOIN ""{schema}"".sp_yard_zone     z  ON z.zone_id = i.zone_id
            LEFT JOIN ""{schema}"".sp_yard_position p  ON p.position_id = i.position_id";

        public const string GetItems = SelectItem + @"
            WHERE i.is_deleted = FALSE AND i.is_virtual = FALSE
              AND (@quality_status IS NULL OR i.quality_status = @quality_status)
            ORDER BY i.position_id NULLS FIRST, i.stack_index DESC, i.item_id
            LIMIT 500;";

        public const string GetItemById = SelectItem + @"
            WHERE i.item_id = @item_id AND i.is_deleted = FALSE;";

        /// <summary>Items ready to leave: located, not held, not already gone.</summary>
        public const string GetDispatchCandidates = SelectItem + @"
            WHERE i.is_deleted = FALSE AND i.is_virtual = FALSE AND i.position_id IS NOT NULL
              AND i.item_status NOT IN ('DISPATCHED', 'SCRAPPED') AND i.quality_status <> 'LOCKED_QA'
            ORDER BY i.stack_index DESC, i.item_code;";

        /// <summary>The mill campaign: hottest first, then oldest cast, so the heat is spent before it is lost.</summary>
        public const string GetMillCampaign = SelectItem + @"
            WHERE i.is_deleted = FALSE AND i.is_virtual = FALSE AND i.position_id IS NOT NULL
              AND i.item_status NOT IN ('DISPATCHED', 'SCRAPPED') AND i.quality_status <> 'LOCKED_QA'
            ORDER BY CASE " + Temperature + @" WHEN 'HOT' THEN 0 WHEN 'WARM' THEN 1 ELSE 2 END,
                     NULLIF(i.attributes->>'cast_time', '')::timestamp NULLS LAST, i.item_id
            LIMIT @limit;";

        // ── Work orders with everything the screens dereference ──────────────────
        public const string GetWorkOrders = @"
            SELECT w.work_order_id, w.order_type, w.order_status, w.created_on, w.completed_at, w.rework_activity_id, w.flag_reason,
                   i.item_id, i.item_code, i.heat_number, i.quality_status, i.item_status, i.grade_code, i.stack_index,
                   " + Temperature + @" AS temperature_state,
                   sz.zone_id AS source_zone_id, sz.zone_name AS source_zone_name, sz.zone_purpose AS source_zone_purpose,
                   dz.zone_id AS destination_zone_id, dz.zone_name AS destination_zone_name, dz.zone_purpose AS destination_zone_purpose,
                   sp.position_id AS source_position_id, sp.row_index AS source_row_index, sp.col_index AS source_col_index,
                   dp.position_id AS destination_position_id, dp.row_index AS destination_row_index, dp.col_index AS destination_col_index
            FROM ""{schema}"".sp_yard_work_order w
            JOIN ""{schema}"".sp_yard_item i ON i.item_id = w.item_id
            LEFT JOIN ""{schema}"".sp_yard_zone     sz ON sz.zone_id = w.source_zone_id
            LEFT JOIN ""{schema}"".sp_yard_zone     dz ON dz.zone_id = w.destination_zone_id
            LEFT JOIN ""{schema}"".sp_yard_position sp ON sp.position_id = w.source_position_id
            LEFT JOIN ""{schema}"".sp_yard_position dp ON dp.position_id = w.destination_position_id
            WHERE w.is_deleted = FALSE AND w.order_status IN ('PENDING', 'IN_PROGRESS', 'FLAGGED')
            ORDER BY CASE w.order_type WHEN 'DIGGING' THEN 0 WHEN 'DISPATCH' THEN 1 ELSE 2 END, w.created_on;";
    }
}
