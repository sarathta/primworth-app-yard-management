using System.Text.Json;
using System.Text.RegularExpressions;
using Jint;
using Primworth_API.IRepository;
using Primworth_API.Models.Yard;

namespace Primworth_API.Repository.Yard
{
    public sealed partial class YardRuleEngine
    {
        private static readonly Regex ImportLine = new(@"(?m)^\s*import\s.+?;\s*", RegexOptions.Compiled);
        private static readonly Regex ExportKeyword = new(@"\bexport\s+", RegexOptions.Compiled);

        public bool UsesLocationGraph(string? graphJson) => IsBatchGraph(ParseGraph(graphJson));

        public YardLocationChoice SelectLocation(YardItem item, IReadOnlyList<YardCandidatePosition> candidates, string? graphJson)
        {
            var graph = ParseGraph(graphJson);
            if (!IsBatchGraph(graph)) return new YardLocationChoice();
            try
            {
                var output = ExecuteGraph(graph, ToElement(SlabRequest(item, candidates)));
                return ReadChoice(output);
            }
            catch (Exception)
            {
                // A broken script must not strand inbound. The built-in policy matches the same rules.
                return BuiltinChoice(item, candidates);
            }
        }

        internal static JsonElement RunGraph(string? graphJson, JsonElement input)
        {
            var graph = ParseGraph(graphJson);
            if (graph.ValueKind != JsonValueKind.Object)
                throw new InvalidOperationException("The rule graph is empty.");
            return ExecuteGraph(graph, input);
        }

        private static bool IsBatchGraph(JsonElement graph)
        {
            if (graph.ValueKind != JsonValueKind.Object || !graph.TryGetProperty("nodes", out var nodes) || nodes.ValueKind != JsonValueKind.Array)
                return false;
            foreach (var node in nodes.EnumerateArray())
            {
                if (string.Equals(Str(node, "type"), "functionNode", StringComparison.OrdinalIgnoreCase))
                    return true;
            }
            return false;
        }

        private static object SlabRequest(YardItem item, IReadOnlyList<YardCandidatePosition> candidates)
        {
            var grade = string.IsNullOrWhiteSpace(item.grade_code) ? "Standard" : item.grade_code.Trim();
            var length = YardAttributes.Number(item.attributes, "length_mm") ?? item.length_mm;
            var width = YardAttributes.Number(item.attributes, "width_mm") ?? item.width_mm;
            var thickness = YardAttributes.Number(item.attributes, "thickness_mm") ?? item.thickness_mm;
            var typeStacks = string.IsNullOrWhiteSpace(item.type_key) || IsStackable(item.type_key);

            return new
            {
                grade_code = grade,
                length_mm = length,
                width_mm = width,
                thickness_mm = thickness,
                size_profile_id = item.size_profile_id,
                locations = candidates.Select(c =>
                {
                    var style = c.storage_style?.Trim() ?? "";
                    var stacks = typeStacks && (style.Length == 0
                        || style.Equals(YardStorageStyle.Stack, StringComparison.OrdinalIgnoreCase)
                        || style.Equals(YardStorageStyle.Pyramid, StringComparison.OrdinalIgnoreCase));
                    var capacity = c.rule_capacity > 0 ? c.rule_capacity : c.capacity_limit;
                    return new
                    {
                        position_id = c.position_id,
                        zone_id = c.zone_id,
                        zone_name = c.zone_name,
                        row_index = c.row_index,
                        col_index = c.col_index,
                        storage_style = c.storage_style,
                        stackable = stacks,
                        occupied = c.occupied,
                        reserved = c.reserved,
                        capacity,
                        top_grade = string.IsNullOrWhiteSpace(c.top_grade) ? null : c.top_grade.Trim(),
                        top_length_mm = c.top_length_mm,
                        top_width_mm = c.top_width_mm,
                        top_thickness_mm = c.top_thickness_mm,
                        top_size_profile_id = c.top_size_profile_id,
                    };
                }).ToArray(),
            };
        }

        private static YardLocationChoice ReadChoice(JsonElement output)
        {
            var allowed = output.ValueKind == JsonValueKind.Object
                && output.TryGetProperty("allowed", out var flag)
                && (flag.ValueKind == JsonValueKind.True || (flag.ValueKind == JsonValueKind.String && bool.TryParse(flag.GetString(), out var b) && b));
            long? id = null;
            if (output.ValueKind == JsonValueKind.Object
                && output.TryGetProperty("position_id", out var position)
                && position.ValueKind == JsonValueKind.Number
                && position.TryGetInt64(out var n))
                id = n;
            var reason = output.ValueKind == JsonValueKind.Object
                && output.TryGetProperty("reason", out var r) && r.ValueKind == JsonValueKind.String
                ? r.GetString() : null;
            if (!allowed || id == null)
                return new YardLocationChoice { Handled = true, Reason = string.IsNullOrWhiteSpace(reason) ? DefaultDeny : reason };
            return new YardLocationChoice { Handled = true, PositionId = id, Reason = reason };
        }

        private static YardLocationChoice BuiltinChoice(YardItem item, IReadOnlyList<YardCandidatePosition> candidates)
        {
            var allowed = new List<YardCandidatePosition>();
            string? reason = null;
            foreach (var candidate in candidates)
            {
                var decision = Builtin(Facts(item, candidate));
                if (decision.Allowed) allowed.Add(candidate);
                else reason ??= decision.Reason;
            }
            if (allowed.Count == 0)
                return new YardLocationChoice { Handled = true, Reason = reason ?? DefaultDeny };
            var pick = allowed
                .OrderByDescending(c => c.occupied)
                .ThenByDescending(c => c.Free)
                .ThenBy(c => c.row_index)
                .ThenBy(c => c.col_index)
                .First();
            return new YardLocationChoice { Handled = true, PositionId = pick.position_id };
        }

        private static JsonElement ExecuteGraph(JsonElement graph, JsonElement input)
        {
            if (!graph.TryGetProperty("nodes", out var nodes) || !graph.TryGetProperty("edges", out var edges))
                return input;

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

            var ctx = input;
            var seen = new HashSet<string>(StringComparer.Ordinal);
            var queue = new Queue<string>();
            foreach (var start in byId.Values.Where(n => string.Equals(Str(n, "type"), "inputNode", StringComparison.OrdinalIgnoreCase)))
            {
                var id = start.GetProperty("id").GetString()!;
                if (outgoing.TryGetValue(id, out var next))
                    foreach (var n in next) queue.Enqueue(n);
            }

            while (queue.Count > 0)
            {
                var id = queue.Dequeue();
                if (!seen.Add(id) || !byId.TryGetValue(id, out var node)) continue;
                ctx = ApplyNode(node, ctx);
                if (outgoing.TryGetValue(id, out var next))
                    foreach (var n in next) queue.Enqueue(n);
            }
            return ctx;
        }

        private static JsonElement ApplyNode(JsonElement node, JsonElement ctx)
        {
            var type = Str(node, "type");
            if (string.Equals(type, "functionNode", StringComparison.OrdinalIgnoreCase))
                return RunFunction(FunctionSource(node), ctx);
            if (string.Equals(type, "decisionTableNode", StringComparison.OrdinalIgnoreCase)
                && node.TryGetProperty("content", out var content))
                return RunTable(content, ctx);
            return ctx;
        }

        private static string? FunctionSource(JsonElement node)
        {
            if (!node.TryGetProperty("content", out var content)) return null;
            if (content.ValueKind == JsonValueKind.String) return content.GetString();
            return Str(content, "source");
        }

        private static JsonElement RunFunction(string? source, JsonElement input)
        {
            if (string.IsNullOrWhiteSpace(source)) return input;
            var script = ExportKeyword.Replace(ImportLine.Replace(source, ""), "");
            using var engine = new Engine(options =>
            {
                options.Strict();
                options.LimitRecursion(32);
                options.MaxStatements(200_000);
                options.TimeoutInterval(TimeSpan.FromSeconds(2));
            });
            // The node exports `const handler`. Wrapping keeps that binding in scope for the call.
            engine.Execute("function __main(__in) {\n" + script + "\nreturn handler(__in);\n}");
            engine.SetValue("__payload", engine.Evaluate("(" + input.GetRawText() + ")"));
            var encoded = engine.Evaluate("JSON.stringify(__main(__payload))");
            if (!encoded.IsString()) return input;
            var text = encoded.AsString();
            if (string.IsNullOrWhiteSpace(text) || text == "null") return input;
            using var doc = JsonDocument.Parse(text);
            return doc.RootElement.ValueKind == JsonValueKind.Object ? doc.RootElement.Clone() : input;
        }

        private static JsonElement RunTable(JsonElement content, JsonElement ctx)
        {
            var loop = string.Equals(Str(content, "executionMode"), "loop", StringComparison.OrdinalIgnoreCase);
            var pass = BoolProp(content, "passThrough", true);
            if (!loop)
            {
                var scope = Scope(ctx, Str(content, "inputField"));
                var hit = EvaluateTable(content, scope);
                var merged = pass ? MergeObject(scope, hit) : hit ?? scope;
                var path = Str(content, "outputPath");
                return string.IsNullOrEmpty(path) ? merged : WithProperty(ctx, path, merged);
            }

            var field = Str(content, "inputField");
            var rows = string.IsNullOrEmpty(field) ? ctx : Prop(ctx, field);
            if (rows.ValueKind != JsonValueKind.Array) return ctx;
            var collected = Element(writer =>
            {
                writer.WriteStartArray();
                foreach (var row in rows.EnumerateArray())
                {
                    var hit = EvaluateTable(content, row);
                    var merged = pass ? MergeObject(row, hit) : hit ?? row;
                    merged.WriteTo(writer);
                }
                writer.WriteEndArray();
            });
            return WithProperty(ctx, Str(content, "outputPath") ?? field ?? "result", collected);
        }

        private static JsonElement Scope(JsonElement ctx, string? field)
        {
            if (string.IsNullOrEmpty(field)) return ctx;
            var scoped = Prop(ctx, field);
            return scoped.ValueKind == JsonValueKind.Object ? scoped : ctx;
        }

        private static bool BoolProp(JsonElement el, string name, bool fallback)
        {
            if (!el.TryGetProperty(name, out var value) || value.ValueKind is JsonValueKind.Null or JsonValueKind.Undefined)
                return fallback;
            if (value.ValueKind == JsonValueKind.True) return true;
            if (value.ValueKind == JsonValueKind.False) return false;
            return fallback;
        }

        private static JsonElement Prop(JsonElement obj, string name)
            => obj.ValueKind == JsonValueKind.Object && obj.TryGetProperty(name, out var value) ? value : default;

        private static JsonElement MergeObject(JsonElement baseObj, JsonElement? overlay)
        {
            return Element(writer =>
            {
                writer.WriteStartObject();
                var written = new HashSet<string>(StringComparer.Ordinal);
                if (overlay is { ValueKind: JsonValueKind.Object } over)
                {
                    foreach (var property in over.EnumerateObject())
                    {
                        written.Add(property.Name);
                        property.WriteTo(writer);
                    }
                }
                if (baseObj.ValueKind == JsonValueKind.Object)
                {
                    foreach (var property in baseObj.EnumerateObject())
                    {
                        if (written.Contains(property.Name)) continue;
                        property.WriteTo(writer);
                    }
                }
                writer.WriteEndObject();
            });
        }

        private static JsonElement WithProperty(JsonElement obj, string name, JsonElement value)
        {
            return Element(writer =>
            {
                writer.WriteStartObject();
                var replaced = false;
                if (obj.ValueKind == JsonValueKind.Object)
                {
                    foreach (var property in obj.EnumerateObject())
                    {
                        if (property.Name == name)
                        {
                            writer.WritePropertyName(name);
                            value.WriteTo(writer);
                            replaced = true;
                        }
                        else property.WriteTo(writer);
                    }
                }
                if (!replaced)
                {
                    writer.WritePropertyName(name);
                    value.WriteTo(writer);
                }
                writer.WriteEndObject();
            });
        }

        private static JsonElement ToElement(object value)
        {
            var json = JsonSerializer.Serialize(value);
            using var doc = JsonDocument.Parse(json);
            return doc.RootElement.Clone();
        }

        private static JsonElement Element(Action<Utf8JsonWriter> write)
        {
            using var stream = new MemoryStream();
            using (var writer = new Utf8JsonWriter(stream))
                write(writer);
            using var doc = JsonDocument.Parse(stream.ToArray());
            return doc.RootElement.Clone();
        }
    }
}
