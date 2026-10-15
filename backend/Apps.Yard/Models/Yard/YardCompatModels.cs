namespace Primworth_API.Models.Yard
{
    // Rows read for the embedded mSYMS screens. Flat here; the compat controllers nest them into
    // the Prisma-style objects the screens dereference.

    /// <summary>An item with the two compat-only fields: the area's purpose and the computed heat state.</summary>
    public class YardCompatItem : YardItem
    {
        public string? zone_purpose { get; set; }
        /// <summary>HOT / WARM / COLD, computed from the cast_time attribute at read time.</summary>
        public string? temperature_state { get; set; }
    }

    public class YardCompatZoneRow
    {
        public long zone_id { get; set; }
        public string? zone_name { get; set; }
        public string? zone_purpose { get; set; }
        public string? storage_style { get; set; }
        public bool is_active { get; set; }
    }

    public class YardCompatPositionRow
    {
        public long position_id { get; set; }
        public long zone_id { get; set; }
        public int row_index { get; set; }
        public int col_index { get; set; }
        public int capacity_limit { get; set; }
    }

    public class YardCompatItemRow
    {
        public long item_id { get; set; }
        public long position_id { get; set; }
        public string? item_code { get; set; }
        public string? heat_number { get; set; }
        public int stack_index { get; set; }
        public string? item_status { get; set; }
        public string? quality_status { get; set; }
        public string? grade_code { get; set; }
        public string? temperature_state { get; set; }
        public string? type_key { get; set; }
    }

    public class YardCompatOverview
    {
        public List<YardCompatZoneRow> zones { get; set; } = new();
        public List<YardCompatPositionRow> positions { get; set; } = new();
        public List<YardCompatItemRow> items { get; set; } = new();
    }

    public class YardCompatWorkOrderRow
    {
        public long work_order_id { get; set; }
        public string? order_type { get; set; }
        public string? order_status { get; set; }
        public DateTime? created_on { get; set; }
        public DateTime? completed_at { get; set; }
        public long? rework_activity_id { get; set; }
        public string? flag_reason { get; set; }
        public long item_id { get; set; }
        public string? item_code { get; set; }
        public string? heat_number { get; set; }
        public string? quality_status { get; set; }
        public string? item_status { get; set; }
        public string? grade_code { get; set; }
        public int? stack_index { get; set; }
        public string? temperature_state { get; set; }
        public long? source_zone_id { get; set; }
        public string? source_zone_name { get; set; }
        public string? source_zone_purpose { get; set; }
        public long? destination_zone_id { get; set; }
        public string? destination_zone_name { get; set; }
        public string? destination_zone_purpose { get; set; }
        public long? source_position_id { get; set; }
        public int? source_row_index { get; set; }
        public int? source_col_index { get; set; }
        public long? destination_position_id { get; set; }
        public int? destination_row_index { get; set; }
        public int? destination_col_index { get; set; }
    }
}
