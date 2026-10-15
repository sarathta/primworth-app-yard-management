namespace Primworth_API.Models.Yard
{
    /// <summary>What an area is for. Mirrors the CHECK constraint on sp_yard_zone.</summary>
    public static class YardZonePurpose
    {
        public const string Storage = "STORAGE";
        public const string Rework = "REWORK";
        public const string Dispatch = "DISPATCH";
        public const string Shuffling = "SHUFFLING";
        public static readonly string[] All = { Storage, Rework, Dispatch, Shuffling };
    }

    /// <summary>
    /// How items sit in an area. The one concept doing the real work in a generic yard:
    ///   STACK   on top of each other; only the top is reachable, the rest become dig orders.
    ///   RACK    one item per addressed slot; everything reachable directly.
    ///   FLOOR   a marked area with a count and no addresses; one position holds the whole capacity.
    ///   PYRAMID rows resting on rows; only the top row is free, the rows beneath are locked.
    /// </summary>
    public static class YardStorageStyle
    {
        public const string Stack = "STACK";
        public const string Rack = "RACK";
        public const string Floor = "FLOOR";
        public const string Pyramid = "PYRAMID";
        public static readonly string[] All = { Stack, Rack, Floor, Pyramid };

        /// <summary>Whether taking one item out can require moving others first.</summary>
        public static bool Digs(string? style) => style is Stack or Pyramid;

        /// <summary>
        /// The formation a new area uses when the screen does not name one.
        /// Slab, plate and billet stack. A TMT bundle sits in a rack. Coil and wire rod default
        /// to a rack (cradles); floor and pyramid are chosen on the area.
        /// </summary>
        public static string ForMaterial(string? typeKey) => (typeKey ?? "").Trim().ToUpperInvariant() switch
        {
            "TMT_BUNDLE" or "COIL" or "WIRE_ROD" => Rack,
            _ => Stack,
        };
    }

    // Maps "{facility_schema}".sp_yard_zone, plus two counts joined for display.
    public class YardZone
    {
        public long zone_id { get; set; }
        public string zone_name { get; set; } = "";
        public string zone_purpose { get; set; } = YardZonePurpose.Storage;
        public string storage_style { get; set; } = YardStorageStyle.Stack;
        public bool is_active { get; set; }
        public int position_count { get; set; }
        public int total_capacity { get; set; }
        public int occupied { get; set; }

        /// <summary>Comma-joined type keys this area accepts (aggregated in SQL).</summary>
        public string? accepted_types { get; set; }
    }

    // Maps "{facility_schema}".sp_yard_position.
    public class YardPosition
    {
        public long position_id { get; set; }
        public long zone_id { get; set; }
        public int row_index { get; set; }
        public int col_index { get; set; }
        public int capacity_limit { get; set; }
        public int occupied { get; set; }
    }

    /// <summary>
    /// Creates an area with its grid of positions in one go. RACK forces capacity 1 on every slot;
    /// FLOOR ignores rows and cols and makes one position holding capacity_limit.
    /// </summary>
    public class YardZoneInput
    {
        public string zone_name { get; set; } = "";
        public string zone_purpose { get; set; } = YardZonePurpose.Storage;
        public string storage_style { get; set; } = YardStorageStyle.Stack;
        public long? yard_type_id { get; set; }
        public List<string> material_type_keys { get; set; } = new();
        public int rows { get; set; } = 1;
        public int cols { get; set; } = 1;
        public int capacity_limit { get; set; } = 5;

        public string? Validate()
        {
            if (string.IsNullOrWhiteSpace(zone_name) || zone_name.Length > 100) return "zone_name is required (max 100 characters).";
            if (!YardZonePurpose.All.Contains(zone_purpose.Trim().ToUpperInvariant())) return $"zone_purpose must be one of {string.Join(", ", YardZonePurpose.All)}.";
            if (!YardStorageStyle.All.Contains(storage_style.Trim().ToUpperInvariant())) return $"storage_style must be one of {string.Join(", ", YardStorageStyle.All)}.";
            if (material_type_keys.Count == 0) return "An area must accept at least one material type.";
            if (rows is < 1 or > 100 || cols is < 1 or > 100) return "rows and cols must be between 1 and 100.";
            if (capacity_limit is < 0 or > 1000) return "capacity_limit must be between 0 and 1000.";
            return null;
        }
    }

    public class YardCapacityInput
    {
        public int capacity_limit { get; set; }
    }
}
