using System.Text.Json;

namespace Primworth_API.Models.Yard
{
    /// <summary>A row of gm_facility_type in the facility schema.</summary>
    public class YardFacilityType
    {
        public long facility_type_id { get; set; }
        public string facility_type_name { get; set; } = "";
        public string? description { get; set; }
    }

    /// <summary>A row of sp_yard_type: Slab Yard, Coil Yard, and the rest.</summary>
    public class YardType
    {
        public long yard_type_id { get; set; }
        public string yard_type_name { get; set; } = "";
        public string? description { get; set; }
        public bool is_active { get; set; }
    }

    // Maps "{facility_schema}".sp_yard_material_type.
    public class YardMaterialType
    {
        public long material_type_id { get; set; }
        public string type_key { get; set; } = "";
        public string singular { get; set; } = "";
        public string plural { get; set; } = "";
        public string id_label { get; set; } = "";
        public string position_label { get; set; } = "R{row}-C{col}";

        /// <summary>Raw jsonb text: a JSON array of attribute definitions. Read it with YardAttributes.</summary>
        public string attribute_defs { get; set; } = "[]";

        public string? rule_set_key { get; set; }

        /// <summary>gm_facility_type this material belongs to. Null until a facility type is assigned.</summary>
        public long? facility_type_id { get; set; }

        public bool is_active { get; set; }
    }

    /// <summary>One field a material carries, as declared on its material type.</summary>
    public sealed record YardAttributeDef(string Key, string Label, string Type, string? Unit, bool Required);

    /// <summary>
    /// Reads attribute definitions and checks an item's attributes against them.
    ///
    /// This is the line that keeps the attribute bag honest. An item whose attributes name a key
    /// the type does not define, miss a required one, or carry the wrong kind of value is refused
    /// before it is written, so product-mix routing and reports can trust every key they read.
    /// </summary>
    public static class YardAttributes
    {
        private static readonly string[] KnownTypes = { "number", "integer", "text", "datetime", "boolean" };

        public static IReadOnlyList<YardAttributeDef> Parse(string? defsJson)
        {
            var defs = new List<YardAttributeDef>();
            if (string.IsNullOrWhiteSpace(defsJson)) return defs;

            using var doc = JsonDocument.Parse(defsJson);
            if (doc.RootElement.ValueKind != JsonValueKind.Array) return defs;

            foreach (var el in doc.RootElement.EnumerateArray())
            {
                var key = Str(el, "key");
                if (string.IsNullOrWhiteSpace(key)) continue;
                defs.Add(new YardAttributeDef(
                    key.Trim(),
                    Str(el, "label") ?? key,
                    (Str(el, "type") ?? "text").ToLowerInvariant(),
                    Str(el, "unit"),
                    el.TryGetProperty("required", out var r) && r.ValueKind == JsonValueKind.True));
            }
            return defs;
        }

        /// <summary>
        /// Returns null when the attributes fit the definitions, otherwise a message a person can act
        /// on. `normalized` is the JSON to store: only defined keys, in definition order.
        /// </summary>
        public static string? Validate(JsonElement? attributes, IReadOnlyList<YardAttributeDef> defs, out string normalized)
        {
            normalized = "{}";
            var given = new Dictionary<string, JsonElement>(StringComparer.OrdinalIgnoreCase);

            if (attributes is { ValueKind: JsonValueKind.Object } obj)
                foreach (var p in obj.EnumerateObject()) given[p.Name] = p.Value;
            else if (attributes is { ValueKind: not (JsonValueKind.Null or JsonValueKind.Undefined) })
                return "attributes must be a JSON object.";

            var known = new HashSet<string>(defs.Select(d => d.Key), StringComparer.OrdinalIgnoreCase);
            var unknown = given.Keys.Where(k => !known.Contains(k)).ToList();
            if (unknown.Count > 0)
                return $"Unknown attribute(s) for this material: {string.Join(", ", unknown)}.";

            var kept = new Dictionary<string, object?>();
            foreach (var def in defs)
            {
                if (!given.TryGetValue(def.Key, out var value) || value.ValueKind is JsonValueKind.Null)
                {
                    if (def.Required) return $"{def.Label} is required.";
                    continue;
                }

                var problem = Check(def, value, out var typed);
                if (problem != null) return problem;
                kept[def.Key] = typed;
            }

            normalized = JsonSerializer.Serialize(kept);
            return null;
        }

        private static string? Check(YardAttributeDef def, JsonElement value, out object? typed)
        {
            typed = null;
            switch (def.Type)
            {
                case "number":
                    if (value.ValueKind == JsonValueKind.Number && value.TryGetDecimal(out var n)) { typed = n; return null; }
                    if (value.ValueKind == JsonValueKind.String && decimal.TryParse(value.GetString(), out var ns)) { typed = ns; return null; }
                    return $"{def.Label} must be a number{Unit(def)}.";
                case "integer":
                    if (value.ValueKind == JsonValueKind.Number && value.TryGetInt64(out var i)) { typed = i; return null; }
                    if (value.ValueKind == JsonValueKind.String && long.TryParse(value.GetString(), out var istr)) { typed = istr; return null; }
                    return $"{def.Label} must be a whole number{Unit(def)}.";
                case "datetime":
                    if (value.ValueKind == JsonValueKind.String && DateTime.TryParse(value.GetString(), out var dt)) { typed = dt; return null; }
                    return $"{def.Label} must be a date and time.";
                case "boolean":
                    if (value.ValueKind is JsonValueKind.True or JsonValueKind.False) { typed = value.GetBoolean(); return null; }
                    return $"{def.Label} must be true or false.";
                default:
                    // "text", and any type the definition misspelt: stored as the text it came as.
                    typed = value.ValueKind == JsonValueKind.String ? value.GetString() : value.ToString();
                    return null;
            }
        }

        private static string Unit(YardAttributeDef def) => string.IsNullOrWhiteSpace(def.Unit) ? "" : $" in {def.Unit}";

        private static string? Str(JsonElement el, string name)
            => el.TryGetProperty(name, out var v) && v.ValueKind == JsonValueKind.String ? v.GetString() : null;

        /// <summary>One attribute as a decimal, or null. Used by placement rules (width, size).</summary>
        public static decimal? Number(string? attributesJson, string key)
        {
            if (string.IsNullOrWhiteSpace(attributesJson)) return null;
            try
            {
                using var doc = JsonDocument.Parse(attributesJson);
                if (!doc.RootElement.TryGetProperty(key, out var el)) return null;
                if (el.ValueKind == JsonValueKind.Number && el.TryGetDecimal(out var n)) return n;
                if (el.ValueKind == JsonValueKind.String && decimal.TryParse(el.GetString(), out var s)) return s;
            }
            catch (JsonException) { /* malformed is "unknown", not a crash in the allocator */ }
            return null;
        }
    }
}
