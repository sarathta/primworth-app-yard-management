namespace Primworth_API.Queries.Yard
{
    /// <summary>SQL for work orders and the movement log.</summary>
    public static class YardWorkOrderQueries
    {
        private const string Select = @"
            SELECT w.work_order_id, w.order_type, w.item_id, w.source_zone_id, w.source_position_id,
                   w.destination_zone_id, w.destination_position_id, w.order_status, w.assigned_user_id,
                   w.flag_reason, w.rework_activity_id, w.completed_at, w.created_on,
                   i.item_code, i.heat_number, i.quality_status, mt.type_key,
                   sz.zone_name AS source_zone_name, dz.zone_name AS destination_zone_name,
                   CASE WHEN sp.position_id IS NULL THEN NULL ELSE 'R' || sp.row_index || '-C' || sp.col_index END AS source_position,
                   CASE WHEN dp.position_id IS NULL THEN NULL ELSE 'R' || dp.row_index || '-C' || dp.col_index END AS destination_position
            FROM ""{schema}"".sp_yard_work_order w
            JOIN ""{schema}"".sp_yard_item i            ON i.item_id = w.item_id
            JOIN ""{schema}"".sp_yard_material_type mt  ON mt.material_type_id = i.material_type_id
            LEFT JOIN ""{schema}"".sp_yard_zone sz      ON sz.zone_id = w.source_zone_id
            LEFT JOIN ""{schema}"".sp_yard_zone dz      ON dz.zone_id = w.destination_zone_id
            LEFT JOIN ""{schema}"".sp_yard_position sp  ON sp.position_id = w.source_position_id
            LEFT JOIN ""{schema}"".sp_yard_position dp  ON dp.position_id = w.destination_position_id";

        /// <summary>The operator worklist. DIGGING first: a dig exists because something is blocked behind it.</summary>
        public const string GetActiveOrders = Select + @"
            WHERE w.is_deleted = FALSE AND w.order_status IN ('PENDING', 'IN_PROGRESS', 'FLAGGED')
              AND (@order_type IS NULL OR w.order_type = @order_type)
            ORDER BY CASE w.order_type WHEN 'DIGGING' THEN 0 WHEN 'DISPATCH' THEN 1 WHEN 'INBOUND' THEN 2 ELSE 3 END,
                     w.created_on;";

        public const string GetById = Select + @"
            WHERE w.work_order_id = @work_order_id AND w.is_deleted = FALSE;";

        public const string Insert = @"
            INSERT INTO ""{schema}"".sp_yard_work_order
                (order_type, item_id, source_zone_id, source_position_id, destination_zone_id, destination_position_id,
                 order_status, rework_activity_id, created_by, created_on, updated_by, updated_on)
            VALUES
                (@order_type, @item_id, @source_zone_id, @source_position_id, @destination_zone_id, @destination_position_id,
                 'PENDING', @rework_activity_id, @user_id, NOW(), @user_id, NOW())
            RETURNING work_order_id;";

        public const string Complete = @"
            UPDATE ""{schema}"".sp_yard_work_order
               SET order_status = 'COMPLETED', completed_at = NOW(),
                   assigned_user_id = COALESCE(assigned_user_id, @user_id), updated_by = @user_id, updated_on = NOW()
             WHERE work_order_id = @work_order_id AND is_deleted = FALSE AND order_status <> 'COMPLETED';";

        public const string Flag = @"
            UPDATE ""{schema}"".sp_yard_work_order
               SET order_status = 'FLAGGED', flag_reason = @reason,
                   assigned_user_id = COALESCE(assigned_user_id, @user_id), updated_by = @user_id, updated_on = NOW()
             WHERE work_order_id = @work_order_id AND is_deleted = FALSE AND order_status <> 'COMPLETED';";

        public const string InsertMovement = @"
            INSERT INTO ""{schema}"".sp_yard_movement_log
                (item_id, from_zone_id, to_zone_id, from_zone_name, to_zone_name, from_position, to_position,
                 order_type, performed_by, performed_name, log_timestamp)
            VALUES
                (@item_id, @from_zone_id, @to_zone_id, @from_zone_name, @to_zone_name, @from_position, @to_position,
                 @order_type, @performed_by, @performed_name, NOW());";

        private const string SelectMovement = @"
            SELECT l.movement_log_id, l.item_id, i.item_code, i.heat_number,
                   l.from_zone_name, l.to_zone_name, l.from_position, l.to_position,
                   l.order_type, l.performed_name, l.log_timestamp
            FROM ""{schema}"".sp_yard_movement_log l
            LEFT JOIN ""{schema}"".sp_yard_item i ON i.item_id = l.item_id";

        public const string GetActivityFeed = SelectMovement + @"
            ORDER BY l.log_timestamp DESC LIMIT @limit;";

        public const string GetItemHistory = SelectMovement + @"
            WHERE l.item_id = @item_id ORDER BY l.log_timestamp DESC;";

        public const string GetUserDisplayName = @"
            SELECT CONCAT(first_name, ' ', last_name) FROM public.sys_users WHERE id = @user_id;";
    }
}
