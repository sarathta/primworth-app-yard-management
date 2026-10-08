namespace Primworth_API.Models.Yard
{
    // Maps "{facility_schema}".sp_yard_size_profile.
    public class YardSizeProfile
    {
        public long size_profile_id { get; set; }
        public string profile_name { get; set; } = "";
        public long? material_type_id { get; set; }
        public decimal length_mm { get; set; }
        public decimal width_mm { get; set; }
        public decimal thickness_mm { get; set; }
        public decimal tolerance_pct { get; set; }
        public bool is_active { get; set; }
    }

    public class YardSizeProfileInput
    {
        public string profile_name { get; set; } = "";
        public decimal length_mm { get; set; }
        public decimal width_mm { get; set; }
        public decimal thickness_mm { get; set; }
        public decimal tolerance_pct { get; set; } = 0.5m;
        public bool is_active { get; set; } = true;

        public string? Validate()
            => string.IsNullOrWhiteSpace(profile_name) || profile_name.Length > 100 ? "A profile name is required (max 100 characters)." : null;
    }

    // Maps "{facility_schema}".sp_yard_rework_activity.
    public class YardReworkActivity
    {
        public long rework_activity_id { get; set; }
        public string activity_name { get; set; } = "";
        public string? description { get; set; }
        public bool is_active { get; set; }
    }

    public class YardReworkActivityInput
    {
        public string activity_name { get; set; } = "";
        public string? description { get; set; }
        public bool is_active { get; set; } = true;

        public string? Validate()
            => string.IsNullOrWhiteSpace(activity_name) || activity_name.Length > 100 ? "An activity name is required (max 100 characters)." : null;
    }

    /// <summary>A product mix with its allowed grades and sizes, children aggregated in SQL as CSV.</summary>
    public class YardProductMix
    {
        public long product_mix_id { get; set; }
        public string mix_name { get; set; } = "";
        public string? description { get; set; }
        public long? material_type_id { get; set; }
        public string target_zone_purpose { get; set; } = YardZonePurpose.Storage;
        public int priority { get; set; }
        public bool is_active { get; set; }
        public string? allowed_grades_str { get; set; }
        public string? allowed_size_ids_str { get; set; }
        public string? allowed_size_names_str { get; set; }

        public List<string> AllowedGrades => Split(allowed_grades_str);
        public List<long> AllowedSizeIds => Split(allowed_size_ids_str).Select(v => long.TryParse(v, out var n) ? n : 0).Where(n => n > 0).ToList();

        internal static List<string> Split(string? csv)
            => string.IsNullOrWhiteSpace(csv) ? new List<string>() : csv.Split(',', StringSplitOptions.RemoveEmptyEntries).Select(v => v.Trim()).ToList();
    }

    public class YardProductMixInput
    {
        public string mix_name { get; set; } = "";
        public string? description { get; set; }
        public long? material_type_id { get; set; }
        public string target_zone_purpose { get; set; } = YardZonePurpose.Storage;
        public int priority { get; set; }
        public bool is_active { get; set; } = true;
        public List<string> allowed_grades { get; set; } = new();
        public List<long> allowed_size_profile_ids { get; set; } = new();

        public string? Validate()
        {
            if (string.IsNullOrWhiteSpace(mix_name) || mix_name.Length > 100) return "A mix name is required (max 100 characters).";
            if (!YardZonePurpose.All.Contains(target_zone_purpose.Trim().ToUpperInvariant())) return $"target_zone_purpose must be one of {string.Join(", ", YardZonePurpose.All)}.";
            if (allowed_grades.Count == 0 && allowed_size_profile_ids.Count == 0) return "A mix needs at least one grade or one size, or it can never match an item.";
            return null;
        }
    }

    public class YardGradeOption
    {
        public long id { get; set; }
        public string? grade_code { get; set; }
        public string? description { get; set; }
    }

    public class YardSystemLogRow
    {
        public long id { get; set; }
        public long? user_id { get; set; }
        public string? action { get; set; }
        public string? details { get; set; }
        public DateTime timestamp { get; set; }
    }
}
