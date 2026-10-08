using System.Text.Json;

namespace Primworth_API.Models.Yard
{
    public static class YardItemStatus
    {
        public const string Pending = "PENDING";
        public const string Ready = "READY";
        public const string Hold = "HOLD";
        public const string Rework = "REWORK";
        public const string Scrapped = "SCRAPPED";
        public const string Dispatched = "DISPATCHED";
    }

    public static class YardQualityStatus
    {
        public const string Pending = "PENDING";
        public const string Approved = "APPROVED";
        /// <summary>Blocks every movement, dispatch included.</summary>
        public const string LockedQa = "LOCKED_QA";
    }

    // Maps "{facility_schema}".sp_yard_item, with the material type and location joined for display.
    public class YardItem
    {
        public long item_id { get; set; }
        public string item_code { get; set; } = "";
        public string? heat_number { get; set; }
        public long material_type_id { get; set; }
        public string? grade_code { get; set; }
        public decimal? weight_kg { get; set; }

        /// <summary>Raw jsonb text. Read single values with YardAttributes.Number.</summary>
        public string attributes { get; set; } = "{}";

        public string item_status { get; set; } = YardItemStatus.Pending;
        public string quality_status { get; set; } = YardQualityStatus.Pending;
        public long? zone_id { get; set; }
        public long? position_id { get; set; }
        public int? stack_index { get; set; }
        public DateTime arrived_on { get; set; }
        public bool is_virtual { get; set; }
        public long? size_profile_id { get; set; }
        public DateTime? updated_on { get; set; }

        // Joined for display
        public string? type_key { get; set; }
        public string? zone_name { get; set; }
        public string? storage_style { get; set; }
        public int? row_index { get; set; }
        public int? col_index { get; set; }

        public string? PositionLabel => row_index.HasValue && col_index.HasValue ? $"R{row_index}-C{col_index}" : null;
    }

    /// <summary>Ingest an item: from a machine feed, a form, or a test.</summary>
    public class YardItemInput
    {
        public string item_code { get; set; } = "";
        public string? heat_number { get; set; }

        /// <summary>Which material this is, by type key. Defaults to the facility's only active type.</summary>
        public string? material_type_key { get; set; }

        public string? grade_code { get; set; }
        public decimal? weight_kg { get; set; }

        /// <summary>A standard size from the size profile master; product-mix routing matches on it.</summary>
        public long? size_profile_id { get; set; }

        /// <summary>A JSON object matching the material type's attribute_defs.</summary>
        public JsonElement? attributes { get; set; }

        /// <summary>Decide where it goes and raise an INBOUND work order. On by default.</summary>
        public bool allocate { get; set; } = true;

        public string? Validate()
        {
            if (string.IsNullOrWhiteSpace(item_code) || item_code.Length > 64) return "item_code is required (max 64 characters).";
            if (heat_number is { Length: > 64 }) return "heat_number must not exceed 64 characters.";
            if (weight_kg is <= 0) return "weight_kg must be greater than zero.";
            return null;
        }
    }

    /// <summary>What happened when an item was ingested, so the caller can explain it.</summary>
    public class YardAllocationResult
    {
        public long item_id { get; set; }
        public bool allocated { get; set; }
        public long? work_order_id { get; set; }
        public string? destination { get; set; }
        public bool held { get; set; }
        public string? reason { get; set; }
    }

    /// <summary>Thrown by repositories on a unique-key collision; controllers answer 409.</summary>
    public class YardDuplicateException : Exception
    {
        public YardDuplicateException(string message) : base(message) { }
    }

    /// <summary>A request the yard refuses for a reason the operator can act on; controllers answer 400.</summary>
    public class YardRuleException : Exception
    {
        public YardRuleException(string message) : base(message) { }
    }
}
