namespace Primworth_API.Models.Yard
{
    /// <summary>Work order kinds. Mirrors the CHECK constraint on sp_yard_work_order.</summary>
    public static class YardOrderType
    {
        public const string Inbound = "INBOUND";
        public const string Move = "MOVE";
        /// <summary>Clearing an item that sits on top of one that is wanted.</summary>
        public const string Digging = "DIGGING";
        public const string Rework = "REWORK";
        public const string Dispatch = "DISPATCH";
        public const string Audit = "AUDIT";
    }

    public static class YardOrderStatus
    {
        public const string Pending = "PENDING";
        public const string InProgress = "IN_PROGRESS";
        public const string Completed = "COMPLETED";
        /// <summary>Raised by an operator who could not carry the order out.</summary>
        public const string Flagged = "FLAGGED";
    }

    // Maps "{facility_schema}".sp_yard_work_order, with item and location joined for display.
    public class YardWorkOrder
    {
        public long work_order_id { get; set; }
        public string order_type { get; set; } = YardOrderType.Move;
        public long item_id { get; set; }
        public long? source_zone_id { get; set; }
        public long? source_position_id { get; set; }
        public long? destination_zone_id { get; set; }
        public long? destination_position_id { get; set; }
        public string order_status { get; set; } = YardOrderStatus.Pending;
        public long? assigned_user_id { get; set; }
        public string? flag_reason { get; set; }
        public long? rework_activity_id { get; set; }
        public DateTime? completed_at { get; set; }
        public DateTime created_on { get; set; }

        // Joined for display
        public string? item_code { get; set; }
        public string? heat_number { get; set; }
        public string? type_key { get; set; }
        public string? quality_status { get; set; }
        public string? source_zone_name { get; set; }
        public string? destination_zone_name { get; set; }
        public string? source_position { get; set; }
        public string? destination_position { get; set; }
    }

    public class YardMovement
    {
        public long movement_log_id { get; set; }
        public long item_id { get; set; }
        public string? item_code { get; set; }
        public string? heat_number { get; set; }
        public string? from_zone_name { get; set; }
        public string? to_zone_name { get; set; }
        public string? from_position { get; set; }
        public string? to_position { get; set; }
        public string? order_type { get; set; }
        public string? performed_name { get; set; }
        public DateTime log_timestamp { get; set; }
    }

    /// <summary>
    /// A position the placement rule considers, with everything it needs to score it: how full
    /// it is (counting orders already promised to it), how it stores things, and the grade on top.
    /// </summary>
    public class YardCandidatePosition
    {
        public long position_id { get; set; }
        public long zone_id { get; set; }
        public string? zone_name { get; set; }
        public string? zone_purpose { get; set; }
        public string? storage_style { get; set; }
        public int row_index { get; set; }
        public int col_index { get; set; }
        public int capacity_limit { get; set; }

        /// <summary>Live items physically here plus open orders heading here.</summary>
        public int occupied { get; set; }

        public string? top_grade { get; set; }
        public string? distinct_grades { get; set; }

        /// <summary>Claimed earlier in the same planning pass. Keeps one plan self-consistent.</summary>
        public int reserved { get; set; }

        public int Free => capacity_limit - occupied - reserved;
        public bool HasRoom => Free > 0;
        public string PositionLabel => $"R{row_index}-C{col_index}";
    }

    public class YardFlagInput
    {
        public string? reason { get; set; }
    }

    public class YardDispatchInput
    {
        public long item_id { get; set; }
        /// <summary>The DISPATCH area to take it to. Null: the first active one.</summary>
        public long? dispatch_zone_id { get; set; }
    }
}
