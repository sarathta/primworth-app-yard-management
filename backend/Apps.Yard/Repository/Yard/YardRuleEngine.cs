using System.Globalization;
using System.Text.Json;
using Primworth_API.IRepository;
using Primworth_API.Models.Yard;

namespace Primworth_API.Repository.Yard
{
    /// <summary>
    /// GoRules JDM evaluator used by inbound placement.
    /// A slab graph reads the grade, length, width and every pit's top slab, then classifies,
    /// validates and picks a location. Older graphs still judge one pit at a time.
    /// </summary>
    public sealed partial class YardRuleEngine : IYardRuleEngine
    {
        private static readonly HashSet<string> StackableTypes = new(StringComparer.OrdinalIgnoreCase)
            { "SLAB", "BILLET", "PLATE" };

        private static readonly string DefaultDeny =
            "Cannot stack: keep the same grade and size, stay within pit capacity, and put longer and wider pieces below.";

        public YardRuleDecision EvaluatePlacement(YardItem item, YardCandidatePosition candidate, string? graphJson)
        {
            var facts = Facts(item, candidate);
            // Grade-family height is a mill limit, not optional graph flavour. An older Slab
            // stacking table with no 400-series row would otherwise allow 439 up to pit capacity.
            if (facts.stackable && facts.stack_used >= facts.grade_max_stack)
                return Deny(YardGradeStacking.DenyReason(new GradeStackLimit(
                    facts.grade_family, facts.grade_max_stack, facts.is_martensitic, facts.is_400_series)));

            var graph = ParseGraph(graphJson);
            if (graph.ValueKind == JsonValueKind.Object && HasDecisionRules(graph))
            {
                try
                {
                    var result = EvaluateGraph(graph, facts);
                    if (result != null) return result;
                }
                catch (JsonException)
                {
                    // A broken graph must not strand inbound; the built-in policy still applies.
                }
            }
            return Builtin(facts);
        }

        public static bool IsStackable(string? typeKey) =>
            !string.IsNullOrWhiteSpace(typeKey) && StackableTypes.Contains(typeKey.Trim());

        internal static YardRuleDecision Builtin(YardStackingFacts facts)
        {
            if (facts.stackable && facts.stack_used >= facts.grade_max_stack)
                return Deny(YardGradeStacking.DenyReason(new GradeStackLimit(facts.grade_family, facts.grade_max_stack, facts.is_martensitic, facts.is_400_series)));
            if (!facts.has_capacity)
                return Deny($"Stack is at capacity ({facts.occupied}/{facts.capacity}).");
            if (!facts.stackable) return new YardRuleDecision { Allowed = true };
            if (facts.occupied == 0) return new YardRuleDecision { Allowed = true };
            if (!facts.same_grade)
                return Deny($"Stack by grade: this pit holds '{facts.top_grade}' and the piece is '{facts.grade}'.");
            if (!facts.same_size)
                return Deny("Stack by size: this pit already holds a different size.");
            if (!facts.length_ok)
                return Deny($"Longer pieces must sit below shorter ones ({facts.length_mm} mm cannot go on {facts.top_length_mm} mm).");
            if (!facts.width_ok)
                return Deny($"Wider pieces must sit below narrower ones ({facts.width_mm} mm cannot go on {facts.top_width_mm} mm).");
            return new YardRuleDecision { Allowed = true };
        }

        private static bool SameSize(long? incomingProfile, long? topProfile, decimal? incomingThickness, decimal? topThickness)
        {
            if (incomingThickness is > 0 && topThickness is > 0)
                return incomingThickness == topThickness;
            if (incomingProfile.HasValue && topProfile.HasValue)
                return incomingProfile == topProfile;
            // The piece already in the pit has no size recorded — grade and pyramid still apply.
            if (!topProfile.HasValue && topThickness is not > 0) return true;
            if (!incomingProfile.HasValue && incomingThickness is not > 0) return true;
            return false;
        }

        private static YardRuleDecision Deny(string reason) => new() { Allowed = false, Reason = reason };

        internal static YardStackingFacts Facts(YardItem item, YardCandidatePosition candidate)
        {
            var stackable = IsStackable(item.type_key)
                && (string.IsNullOrWhiteSpace(candidate.storage_style)
                    || string.Equals(candidate.storage_style, YardStorageStyle.Stack, StringComparison.OrdinalIgnoreCase)
                    || string.Equals(candidate.storage_style, YardStorageStyle.Pyramid, StringComparison.OrdinalIgnoreCase));

            var grade = string.IsNullOrWhiteSpace(item.grade_code) ? "Standard" : item.grade_code.Trim();
            var topGrade = string.IsNullOrWhiteSpace(candidate.top_grade) ? "Standard" : candidate.top_grade.Trim();
            var length = YardAttributes.Number(item.attributes, "length_mm") ?? item.length_mm;
            var width = YardAttributes.Number(item.attributes, "width_mm") ?? item.width_mm;
            var thickness = YardAttributes.Number(item.attributes, "thickness_mm") ?? item.thickness_mm;

            var empty = candidate.occupied <= 0;
            var sameSize = empty || SameSize(item.size_profile_id, candidate.top_size_profile_id, thickness, candidate.top_thickness_mm);
            // Missing top dimensions cannot be compared; only reject when both sides are known and the incoming piece is larger.
            var lengthOk = empty || candidate.top_length_mm is not > 0 || (length is > 0 && length <= candidate.top_length_mm);
            var widthOk = empty || candidate.top_width_mm is not > 0 || (width is > 0 && width <= candidate.top_width_mm);
            var capacity = candidate.rule_capacity > 0 ? candidate.rule_capacity : candidate.capacity_limit;
            var used = candidate.occupied + candidate.reserved;
            var gradeLimit = YardGradeStacking.Classify(grade);

            return new YardStackingFacts
            {
                stackable = stackable,
                occupied = candidate.occupied,
                capacity = capacity,
                has_capacity = capacity > 0 && used < capacity,
                stack_used = used,
                grade = grade,
                top_grade = topGrade,
                same_grade = empty || string.Equals(grade, topGrade, StringComparison.OrdinalIgnoreCase),
                same_size = empty || sameSize,
                length_ok = lengthOk,
                width_ok = widthOk,
                length_mm = length,
                width_mm = width,
                top_length_mm = candidate.top_length_mm,
                top_width_mm = candidate.top_width_mm,
                grade_family = gradeLimit.Family,
                grade_max_stack = gradeLimit.Max,
                is_martensitic = gradeLimit.IsMartensitic,
                is_400_series = gradeLimit.Is400Series,
            };
        }

        private static JsonElement ParseGraph(string? json)
        {
            if (string.IsNullOrWhiteSpace(json)) return default;
            try
            {
                using var doc = JsonDocument.Parse(json);
                return doc.RootElement.Clone();
            }
            catch (JsonException)
            {
                return default;
            }
        }

        private static bool HasDecisionRules(JsonElement graph)
        {
            if (!graph.TryGetProperty("nodes", out var nodes) || nodes.ValueKind != JsonValueKind.Array) return false;
            foreach (var node in nodes.EnumerateArray())
            {
                if (!string.Equals(Str(node, "type"), "decisionTableNode", StringComparison.OrdinalIgnoreCase)) continue;
                if (node.TryGetProperty("content", out var content)
                    && content.TryGetProperty("rules", out var rules)
                    && rules.ValueKind == JsonValueKind.Array
                    && rules.GetArrayLength() > 0)
                    return true;
            }
            return false;
        }

        private static YardRuleDecision? EvaluateGraph(JsonElement graph, YardStackingFacts facts)
        {
            using var payload = JsonDocument.Parse(JsonSerializer.Serialize(facts));
            var ctx = payload.RootElement.Clone();
            if (!graph.TryGetProperty("nodes", out var nodes) || !graph.TryGetProperty("edges", out var edges))
                return null;

            var byId = nodes.EnumerateArray()
                .Where(n => n.TryGetProperty("id", out var id) && id.ValueKind == JsonValueKind.String)
                .ToDictionary(n => n.GetProperty("id").GetString()!, n => n, StringComparer.Ordinal);

            var outgoing = new Dictionary<string, List<string>>(StringComparer.Ordinal);
            foreach (var edge in edges.EnumerateArray())
            {
                var from = Str(edge, "sourceId") ?? Str(edge, "source_id");
                var to = Str(edge, "targetId") ?? Str(edge, "target_id");
                if (from == null || to == null) continue;
                if (!outgoing.TryGetValue(from, out var list)) outgoing[from] = list = new List<string>();
                list.Add(to);
            }

            var starts = byId.Values
                .Where(n => string.Equals(Str(n, "type"), "inputNode", StringComparison.OrdinalIgnoreCase))
                .Select(n => n.GetProperty("id").GetString()!)
                .ToList();

            var seen = new HashSet<string>(StringComparer.Ordinal);
            var queue = new Queue<string>(starts);
            bool? allowed = null;
            string? reason = null;

            while (queue.Count > 0)
            {
                var id = queue.Dequeue();
                if (!seen.Add(id) || !byId.TryGetValue(id, out var node)) continue;
                var type = Str(node, "type");
                if (string.Equals(type, "decisionTableNode", StringComparison.OrdinalIgnoreCase)
                    && node.TryGetProperty("content", out var content))
                {
                    var hit = EvaluateTable(content, ctx);
                    if (hit != null)
                    {
                        if (hit.Value.TryGetProperty("allowed", out var a))
                            allowed = a.ValueKind == JsonValueKind.True || (a.ValueKind == JsonValueKind.String && bool.TryParse(a.GetString(), out var b) && b);
                        if (hit.Value.TryGetProperty("reason", out var r) && r.ValueKind == JsonValueKind.String)
                            reason = r.GetString();
                    }
                }

                if (outgoing.TryGetValue(id, out var next))
                    foreach (var n in next) queue.Enqueue(n);
            }

            if (allowed == null) return null;
            if (allowed == true) return new YardRuleDecision { Allowed = true };
            // The table's catch-all is generic; Builtin names the fact that actually failed.
            return Deny(Builtin(facts).Reason ?? reason ?? DefaultDeny);
        }

        private static JsonElement? EvaluateTable(JsonElement content, JsonElement ctx)
        {
            var hitPolicy = (Str(content, "hitPolicy") ?? "first").ToLowerInvariant();
            if (!content.TryGetProperty("rules", out var rules) || rules.ValueKind != JsonValueKind.Array) return null;
            var inputs = content.TryGetProperty("inputs", out var ins) && ins.ValueKind == JsonValueKind.Array
                ? ins.EnumerateArray().ToList() : new List<JsonElement>();
            var outputs = content.TryGetProperty("outputs", out var outs) && outs.ValueKind == JsonValueKind.Array
                ? outs.EnumerateArray().ToList() : new List<JsonElement>();

            foreach (var rule in rules.EnumerateArray())
            {
                if (rule.ValueKind != JsonValueKind.Object) continue;
                var match = true;
                foreach (var input in inputs)
                {
                    var colId = Str(input, "id");
                    if (colId == null) continue;
                    var cell = Cell(rule, colId);
                    if (string.IsNullOrWhiteSpace(cell)) continue;
                    var field = Str(input, "field");
                    var value = string.IsNullOrWhiteSpace(field) ? ctx : PathValue(ctx, field);
                    if (!UnaryMatch(value, cell)) { match = false; break; }
                }
                if (!match) continue;

                using var stream = new MemoryStream();
                using (var writer = new Utf8JsonWriter(stream))
                {
                    writer.WriteStartObject();
                    foreach (var output in outputs)
                    {
                        var colId = Str(output, "id");
                        var field = Str(output, "field") ?? colId;
                        if (colId == null || field == null) continue;
                        WriteOutput(writer, field, Cell(rule, colId));
                    }
                    writer.WriteEndObject();
                }
                using var doc = JsonDocument.Parse(stream.ToArray());
                var hit = doc.RootElement.Clone();
                if (hitPolicy != "collect") return hit;
            }
            return null;
        }

        private static void WriteOutput(Utf8JsonWriter writer, string field, string? cell)
        {
            var key = field.Split('.')[0];
            var text = (cell ?? "").Trim();
            writer.WritePropertyName(key);
            if (text.Length >= 2 && ((text[0] == '"' && text[^1] == '"') || (text[0] == '\'' && text[^1] == '\'')))
                writer.WriteStringValue(text[1..^1]);
            else if (bool.TryParse(text, out var b)) writer.WriteBooleanValue(b);
            else if (decimal.TryParse(text, NumberStyles.Number, CultureInfo.InvariantCulture, out var n))
                writer.WriteNumberValue(n);
            else writer.WriteStringValue(text);
        }

        private static bool UnaryMatch(JsonElement value, string cell)
        {
            var text = cell.Trim();
            if (text.Length == 0) return true;
            if (TryStructuredMatch(value, text, out var structured)) return structured;

            string? op = null;
            var rest = text;
            foreach (var candidate in new[] { ">=", "<=", "!=", "==", ">", "<" })
            {
                if (!text.StartsWith(candidate, StringComparison.Ordinal)) continue;
                op = candidate;
                rest = text[candidate.Length..].Trim();
                break;
            }

            var expected = ParseLiteral(rest);
            var actual = FromJson(value);
            var cmp = Compare(actual, expected);

            return op switch
            {
                null => EqualsValue(actual, expected),
                "==" => EqualsValue(actual, expected),
                "!=" => !EqualsValue(actual, expected),
                ">" => cmp > 0,
                ">=" => cmp >= 0,
                "<" => cmp < 0,
                "<=" => cmp <= 0,
                _ => false,
            };
        }

        /// <summary>
        /// Zen unary forms a grade table needs when a new code is added:
        /// contains("mint"), [403, 410, 440], [400..499].
        /// </summary>
        private static bool TryStructuredMatch(JsonElement value, string text, out bool matched)
        {
            matched = false;
            if (TryCall(text, "contains", out var arg))
            {
                matched = (AsText(value) ?? "").Contains(arg, StringComparison.OrdinalIgnoreCase);
                return true;
            }
            if (TryCall(text, "startswith", out arg))
            {
                matched = (AsText(value) ?? "").StartsWith(arg, StringComparison.OrdinalIgnoreCase);
                return true;
            }
            if (TryCall(text, "endswith", out arg))
            {
                matched = (AsText(value) ?? "").EndsWith(arg, StringComparison.OrdinalIgnoreCase);
                return true;
            }
            if (text.Length >= 2 && text[0] == '[' && text[^1] == ']')
            {
                matched = InListOrRange(value, text[1..^1].Trim());
                return true;
            }
            return false;
        }

        private static bool TryCall(string text, string name, out string arg)
        {
            arg = "";
            if (!text.StartsWith(name, StringComparison.OrdinalIgnoreCase)) return false;
            var rest = text[name.Length..].TrimStart();
            if (rest.Length < 2 || rest[0] != '(' || rest[^1] != ')') return false;
            var parsed = ParseLiteral(rest[1..^1].Trim());
            arg = Convert.ToString(parsed, CultureInfo.InvariantCulture) ?? "";
            return true;
        }

        private static bool InListOrRange(JsonElement value, string body)
        {
            var dots = body.IndexOf("..", StringComparison.Ordinal);
            if (dots >= 0 && !body.Contains(','))
            {
                var actual = AsDecimal(FromJson(value));
                if (actual == null) return false;
                if (!decimal.TryParse(body[..dots].Trim(), NumberStyles.Number, CultureInfo.InvariantCulture, out var lo)) return false;
                if (!decimal.TryParse(body[(dots + 2)..].Trim(), NumberStyles.Number, CultureInfo.InvariantCulture, out var hi)) return false;
                return actual.Value >= lo && actual.Value <= hi;
            }

            var actualValue = FromJson(value);
            foreach (var part in body.Split(',', StringSplitOptions.RemoveEmptyEntries | StringSplitOptions.TrimEntries))
            {
                if (EqualsValue(actualValue, ParseLiteral(part))) return true;
            }
            return false;
        }

        private static string? AsText(JsonElement value) => value.ValueKind switch
        {
            JsonValueKind.String => value.GetString(),
            JsonValueKind.Number => value.ToString(),
            JsonValueKind.True => "true",
            JsonValueKind.False => "false",
            _ => null,
        };

        private static object? ParseLiteral(string text)
        {
            if (text.Length >= 2 && ((text[0] == '"' && text[^1] == '"') || (text[0] == '\'' && text[^1] == '\'')))
                return text[1..^1];
            if (bool.TryParse(text, out var b)) return b;
            if (decimal.TryParse(text, NumberStyles.Number, CultureInfo.InvariantCulture, out var n)) return n;
            return text;
        }

        private static object? FromJson(JsonElement el) => el.ValueKind switch
        {
            JsonValueKind.True => true,
            JsonValueKind.False => false,
            JsonValueKind.Number when el.TryGetDecimal(out var n) => n,
            JsonValueKind.String => el.GetString(),
            JsonValueKind.Null or JsonValueKind.Undefined => null,
            _ => el.ToString(),
        };

        private static bool EqualsValue(object? left, object? right)
        {
            if (left is bool lb && right is bool rb) return lb == rb;
            if (left is decimal || right is decimal)
                return AsDecimal(left) is { } a && AsDecimal(right) is { } b && a == b;
            return string.Equals(Convert.ToString(left, CultureInfo.InvariantCulture),
                Convert.ToString(right, CultureInfo.InvariantCulture), StringComparison.OrdinalIgnoreCase);
        }

        private static int Compare(object? left, object? right)
        {
            var a = AsDecimal(left);
            var b = AsDecimal(right);
            if (a == null || b == null) return 0;
            return a.Value.CompareTo(b.Value);
        }

        private static decimal? AsDecimal(object? value) => value switch
        {
            decimal d => d,
            int i => i,
            long l => l,
            bool b => b ? 1 : 0,
            string s when decimal.TryParse(s, NumberStyles.Number, CultureInfo.InvariantCulture, out var n) => n,
            _ => null,
        };

        private static JsonElement PathValue(JsonElement root, string path)
        {
            var cur = root;
            foreach (var part in path.Split('.', StringSplitOptions.RemoveEmptyEntries))
            {
                if (cur.ValueKind != JsonValueKind.Object || !cur.TryGetProperty(part, out cur))
                    return default;
            }
            return cur;
        }

        private static string? Cell(JsonElement rule, string colId)
        {
            if (!rule.TryGetProperty(colId, out var v)) return null;
            return v.ValueKind switch
            {
                JsonValueKind.String => v.GetString(),
                JsonValueKind.Number => v.ToString(),
                JsonValueKind.True => "true",
                JsonValueKind.False => "false",
                _ => null,
            };
        }

        private static string? Str(JsonElement el, string name)
            => el.TryGetProperty(name, out var v) && v.ValueKind == JsonValueKind.String ? v.GetString() : null;
    }

    internal sealed class YardStackingFacts
    {
        public bool stackable { get; init; }
        public int occupied { get; init; }
        public int capacity { get; init; }
        public bool has_capacity { get; init; }
        public string grade { get; init; } = "";
        public string top_grade { get; init; } = "";
        public bool same_grade { get; init; }
        public bool same_size { get; init; }
        public bool length_ok { get; init; }
        public bool width_ok { get; init; }
        public decimal? length_mm { get; init; }
        public decimal? width_mm { get; init; }
        public decimal? top_length_mm { get; init; }
        public decimal? top_width_mm { get; init; }
        public int stack_used { get; init; }
        public string grade_family { get; init; } = YardGradeStacking.Normal;
        public int grade_max_stack { get; init; } = YardGradeStacking.NormalMax;
        public bool is_martensitic { get; init; }
        public bool is_400_series { get; init; }
    }
}
