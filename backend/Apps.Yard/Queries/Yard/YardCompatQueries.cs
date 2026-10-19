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

        /// <summary>Null @type_key means every material. Used by queries that already join mt.</summary>
        private const string MatchesType = "(@type_key IS NULL OR LOWER(mt.type_key) = LOWER(@type_key))";

        /// <summary>Areas that accept @type_key. {zone} is the zone id column (z.zone_id, p.zone_id).</summary>
        private const string ZoneAcceptsType = @"
            (@type_key IS NULL OR EXISTS (
                SELECT 1
                FROM ""{schema}"".sp_yard_zone_material_type zm
                JOIN ""{schema}"".sp_yard_material_type mt ON mt.material_type_id = zm.material_type_id
                WHERE zm.zone_id = {zone}
                  AND mt.is_deleted = FALSE
                  AND LOWER(mt.type_key) = LOWER(@type_key)))";

        /// <summary>
        /// The zone alias is the yard open on the left. Null @yard_type_id means no yard was
        /// chosen. A chosen yard never includes another yard's areas, and never includes an
        /// area that has not been assigned to a yard.
        /// </summary>
        private static string InYard(string alias) =>
            "(@yard_type_id IS NULL OR " + alias + ".yard_type_id = @yard_type_id)";

        /// <summary>Same rule as InYard, for a row that only has a zone id.</summary>
        private static string ZoneIdInYard(string zoneIdExpr) =>
            "(@yard_type_id IS NULL OR EXISTS (" +
            "SELECT 1 FROM \"{schema}\".sp_yard_zone yz WHERE yz.zone_id = " + zoneIdExpr +
            " AND yz.is_deleted = FALSE AND yz.yard_type_id = @yard_type_id))";

        // ── Yard overview: three flat queries, assembled in memory ───────────────
        public static readonly string OverviewZones = @"
            SELECT z.zone_id, z.zone_name, z.zone_purpose, z.storage_style, z.is_active
            FROM ""{schema}"".sp_yard_zone z
            WHERE z.is_deleted = FALSE
              AND " + ZoneAcceptsType.Replace("{zone}", "z.zone_id") + @"
              AND " + InYard("z") + @"
            ORDER BY CASE z.zone_purpose WHEN 'STORAGE' THEN 0 WHEN 'REWORK' THEN 1 WHEN 'DISPATCH' THEN 2 ELSE 3 END, z.zone_name;";

        public static readonly string OverviewPositions = @"
            SELECT p.position_id, p.zone_id, p.row_index, p.col_index, p.capacity_limit
            FROM ""{schema}"".sp_yard_position p
            WHERE p.is_deleted = FALSE
              AND " + ZoneAcceptsType.Replace("{zone}", "p.zone_id") + @"
              AND " + ZoneIdInYard("p.zone_id") + @"
            ORDER BY p.row_index, p.col_index;";

        public static readonly string OverviewItems = @"
            SELECT i.item_id, i.position_id, i.item_code, i.heat_number, i.stack_index, i.item_status, i.quality_status,
                   i.grade_code, " + Temperature + @" AS temperature_state, mt.type_key
            FROM ""{schema}"".sp_yard_item i
            LEFT JOIN ""{schema}"".sp_yard_material_type mt ON mt.material_type_id = i.material_type_id AND mt.is_deleted = FALSE
            WHERE i.is_deleted = FALSE AND i.position_id IS NOT NULL AND i.is_virtual = FALSE
              AND (@type_key IS NULL OR EXISTS (
                    SELECT 1 FROM ""{schema}"".sp_yard_material_type mt
                    WHERE mt.material_type_id = i.material_type_id
                      AND mt.is_deleted = FALSE
                      AND LOWER(mt.type_key) = LOWER(@type_key)))
              AND " + ZoneIdInYard("i.zone_id") + @"
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

        public static readonly string GetItems = SelectItem + @"
            WHERE i.is_deleted = FALSE AND i.is_virtual = FALSE
              AND (@quality_status IS NULL OR i.quality_status = @quality_status)
              AND " + MatchesType + @"
              AND " + InYard("z") + @"
            ORDER BY i.position_id NULLS FIRST, i.stack_index DESC, i.item_id
            LIMIT 500;";

        public const string GetItemById = SelectItem + @"
            WHERE i.item_id = @item_id AND i.is_deleted = FALSE;";

        /// <summary>Items ready to leave: located, not held, not already gone.</summary>
        public static readonly string GetDispatchCandidates = SelectItem + @"
            WHERE i.is_deleted = FALSE AND i.is_virtual = FALSE AND i.position_id IS NOT NULL
              AND i.item_status NOT IN ('DISPATCHED', 'SCRAPPED') AND i.quality_status <> 'LOCKED_QA'
              AND " + MatchesType + @"
              AND " + InYard("z") + @"
            ORDER BY i.stack_index DESC, i.item_code;";

        /// <summary>The mill campaign: hottest first, then oldest cast, so the heat is spent before it is lost.</summary>
        public static readonly string GetMillCampaign = SelectItem + @"
            WHERE i.is_deleted = FALSE AND i.is_virtual = FALSE AND i.position_id IS NOT NULL
              AND i.item_status NOT IN ('DISPATCHED', 'SCRAPPED') AND i.quality_status <> 'LOCKED_QA'
              AND " + MatchesType + @"
              AND " + InYard("z") + @"
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
              AND (@type_key IS NULL OR EXISTS (
                    SELECT 1 FROM ""{schema}"".sp_yard_material_type mt
                    WHERE mt.material_type_id = i.material_type_id
                      AND mt.is_deleted = FALSE
                      AND LOWER(mt.type_key) = LOWER(@type_key)))
              -- The item's own zone is not what places the order. A pickup belongs to the source
              -- yard. An external entry has no source; it belongs to the yard of its destination.
              AND (@yard_type_id IS NULL
                   OR sz.yard_type_id = @yard_type_id
                   OR (w.source_zone_id IS NULL AND dz.yard_type_id = @yard_type_id))
            ORDER BY CASE w.order_type WHEN 'DIGGING' THEN 0 WHEN 'DISPATCH' THEN 1 ELSE 2 END, w.created_on;";
    }
}
