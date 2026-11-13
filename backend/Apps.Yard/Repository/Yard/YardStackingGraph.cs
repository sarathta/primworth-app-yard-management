using System.Text.Json;

namespace Primworth_API.Repository.Yard
{
    /// <summary>Default GoRules graphs: capacity, grade, size, longer/wider below.</summary>
    public static class YardStackingGraph
    {
        public const string SlabYard = "Slab Yard";
        public const string BilletYard = "Billet Yard";

        /// <summary>
        /// Slab graph: read the grade, classify it, check every pit against the top slab, pick one.
        /// A new grade is a row in Grade Classification. Families stay martensitic / 430 Mint (5),
        /// 400-series (10) and everything else (15).
        /// </summary>
        public static string Slab => _slab ??= BuildSlab();
        private static string? _slab;

        private static string BuildSlab()
        {
            var graph = new
            {
                nodes = new object[]
                {
                    Node("inputNode", RequestId, "request", 40, 160, new { schema = RequestSchema }),
                    Node("functionNode", ReadId, "Read grade", 300, 160, new { source = ReadGradeSource }),
                    Table(ClassId, "Grade Classification", 620, 80, "single", null, null, GradeInputs, GradeOutputs, GradeRules),
                    Node("functionNode", FactsId, "Stack facts", 1100, 160, new { source = StackFactsSource }),
                    Table(ValidId, "Stack Validation", 1460, 80, "loop", "locations", "locations", StackInputs, StackOutputs, StackRules),
                    Node("functionNode", SelectId, "Location Selection", 1960, 160, new { source = SelectSource }),
                    Node("outputNode", ResponseId, "response", 2300, 160, new { schema = ResponseSchema }),
                },
                edges = new object[]
                {
                    Edge("e-slab-1", RequestId, ReadId),
                    Edge("e-slab-2", ReadId, ClassId),
                    Edge("e-slab-3", ClassId, FactsId),
                    Edge("e-slab-4", FactsId, ValidId),
                    Edge("e-slab-5", ValidId, SelectId),
                    Edge("e-slab-6", SelectId, ResponseId),
                }
            };
            return JsonSerializer.Serialize(graph, new JsonSerializerOptions { WriteIndented = true });
        }

        private const string RequestId = "6f0a1c10-7b2e-4a11-9c01-a10000000001";
        private const string ReadId = "6f0a1c10-7b2e-4a11-9c01-a10000000002";
        private const string ClassId = "6f0a1c10-7b2e-4a11-9c01-a10000000003";
        private const string FactsId = "6f0a1c10-7b2e-4a11-9c01-a10000000004";
        private const string ValidId = "6f0a1c10-7b2e-4a11-9c01-a10000000005";
        private const string SelectId = "6f0a1c10-7b2e-4a11-9c01-a10000000006";
        private const string ResponseId = "6f0a1c10-7b2e-4a11-9c01-a10000000007";

        private const string RequestSchema = """
            {"type":"object","properties":{"grade_code":{"type":"string"},"length_mm":{"type":"number"},"width_mm":{"type":"number"},"thickness_mm":{"type":"number"},"size_profile_id":{"type":"number"},"locations":{"type":"array","description":"Pits and the slab currently on top","items":{"type":"object","properties":{"position_id":{"type":"number"},"occupied":{"type":"number"},"capacity":{"type":"number"},"top_grade":{"type":"string"},"top_length_mm":{"type":"number"},"top_width_mm":{"type":"number"},"top_thickness_mm":{"type":"number"},"top_size_profile_id":{"type":"number"}}}}}}
            """;

        private const string ResponseSchema = """
            {"type":"object","properties":{"allowed":{"type":"boolean"},"position_id":{"type":"number"},"reason":{"type":"string"}}}
            """;

        // Columns only. A new grade is a row in Grade Classification, not a change here.
        private const string ReadGradeSource = """
            import zen from 'zen';

            /** @type {Handler} **/
            export const handler = (input) => {
              const grade = String(input.grade_code == null ? '' : input.grade_code).trim();
              const shown = grade || 'Standard';
              const aisi = shown.match(/(?:^|[^0-9])(\d{3})(?![0-9])/);
              const en = shown.match(/1\.(\d{4})/);
              return {
                ...input,
                grade_code: shown,
                grade_lc: shown.toLowerCase(),
                grade_number: aisi ? Number(aisi[1]) : 0,
                en_number: en ? Number(en[1]) : 0,
                is_ph: /17[\s\-\/]?4|15[\s\-\/]?5|13[\s\-\/]?8/i.test(shown)
              };
            };
            """;

        // Compares the incoming slab with the slab on top of each pit. The table decides what is allowed.
        private const string StackFactsSource = """
            import zen from 'zen';

            /** @type {Handler} **/
            export const handler = (input) => {
              const grade = String(input.grade_code == null ? 'Standard' : input.grade_code).trim() || 'Standard';
              const length = Number(input.length_mm) || 0;
              const width = Number(input.width_mm) || 0;
              const thickness = Number(input.thickness_mm) || 0;
              const sizeId = input.size_profile_id == null || input.size_profile_id === '' ? null : Number(input.size_profile_id);
              const family = input.grade_family || 'normal';
              const maxStack = Number(input.max_stack) || 15;
              const locations = Array.isArray(input.locations) ? input.locations : [];
              const rows = locations.map((loc) => {
                const occupied = Number(loc.occupied) || 0;
                const reserved = Number(loc.reserved) || 0;
                const capacity = Number(loc.capacity) || 0;
                const used = occupied + reserved;
                const empty = used <= 0;
                const topGrade = String(loc.top_grade == null ? '' : loc.top_grade).trim() || 'Standard';
                const topLen = Number(loc.top_length_mm) || 0;
                const topWid = Number(loc.top_width_mm) || 0;
                const topTh = Number(loc.top_thickness_mm) || 0;
                const topSize = loc.top_size_profile_id == null || loc.top_size_profile_id === '' ? null : Number(loc.top_size_profile_id);
                const style = String(loc.storage_style == null ? '' : loc.storage_style).toUpperCase();
                const stackable = loc.stackable === true || loc.stackable === false
                  ? loc.stackable === true
                  : style === '' || style === 'STACK' || style === 'PYRAMID';
                let sameSize = true;
                if (!empty) {
                  if (thickness > 0 && topTh > 0) sameSize = thickness === topTh;
                  else if (sizeId != null && topSize != null) sameSize = sizeId === topSize;
                  else if (topSize == null && topTh <= 0) sameSize = true;
                  else if (sizeId == null && thickness <= 0) sameSize = true;
                  else sameSize = false;
                }
                return {
                  ...loc,
                  grade_code: grade,
                  grade_family: family,
                  max_stack: maxStack,
                  length_mm: length,
                  width_mm: width,
                  occupied: occupied,
                  capacity: capacity,
                  stack_used: used,
                  stackable: stackable,
                  has_capacity: capacity > 0 && used < capacity,
                  over_limit: stackable && used >= maxStack,
                  same_grade: empty || topGrade.toLowerCase() === grade.toLowerCase(),
                  same_size: sameSize,
                  length_ok: empty || topLen <= 0 || (length > 0 && length <= topLen),
                  width_ok: empty || topWid <= 0 || (width > 0 && width <= topWid)
                };
              });
              return { ...input, locations: rows };
            };
            """;

        // Fill the fullest compatible pit first, then the roomiest, then the lowest row and column.
        private const string SelectSource = """
            import zen from 'zen';

            /** @type {Handler} **/
            export const handler = (input) => {
              const rows = Array.isArray(input.locations) ? input.locations : [];
              const allowed = [];
              let reason = '';
              for (let i = 0; i < rows.length; i++) {
                const row = rows[i];
                if (row.allowed === true) allowed.push(row);
                else if (!reason && row.reason) reason = String(row.reason);
              }
              if (allowed.length === 0) {
                return {
                  allowed: false,
                  position_id: null,
                  reason: reason || 'No free pit satisfies the stacking rules (grade, size, capacity, longer/wider below).'
                };
              }
              allowed.sort((a, b) => {
                const occ = (Number(b.occupied) || 0) - (Number(a.occupied) || 0);
                if (occ !== 0) return occ;
                const freeA = (Number(a.capacity) || 0) - (Number(a.occupied) || 0) - (Number(a.reserved) || 0);
                const freeB = (Number(b.capacity) || 0) - (Number(b.occupied) || 0) - (Number(b.reserved) || 0);
                if (freeB !== freeA) return freeB - freeA;
                const row = (Number(a.row_index) || 0) - (Number(b.row_index) || 0);
                if (row !== 0) return row;
                return (Number(a.col_index) || 0) - (Number(b.col_index) || 0);
              });
              return { allowed: true, position_id: allowed[0].position_id, reason: '' };
            };
            """;

        private static readonly object[] GradeInputs =
        {
            In("in_grade", "Grade text. Add a row for a new grade (exact or contains).", "grade_lc", "string"),
            In("in_ph", "Precipitation-hardening (17-4, 15-5, 13-8)", "is_ph", "boolean"),
            In("in_num", "AISI number inside the grade", "grade_number", "number"),
            In("in_en", "EN number after 1.", "en_number", "number"),
        };

        private static readonly object[] GradeOutputs =
        {
            Out("out_family", "Grade family", "grade_family", "string"),
            Out("out_max", "Max slabs in one stack", "max_stack", "number"),
        };

        private static readonly object[] GradeRules =
        {
            Grade("g-430-mint", "== \"430 mint\"", "", "", "", "martensitic", "5"),
            Grade("g-mint", "contains(\"mint\")", "", "", "", "martensitic", "5"),
            Grade("g-marten", "contains(\"marten\")", "", "", "", "martensitic", "5"),
            Grade("g-ph", "", "true", "", "", "martensitic", "5"),
            Grade("g-aisi-martensitic", "", "", "[403, 410, 414, 416, 420, 422, 431, 440]", "", "martensitic", "5"),
            Grade("g-en-martensitic", "", "", "", "[4006, 4021, 4028, 4031, 4034, 4057, 4104, 4112, 4125]", "martensitic", "5"),
            Grade("g-400", "", "", "[400..499]", "", "400_series", "10"),
            Grade("g-en-400", "", "", "", "[4000..4299]", "400_series", "10"),
            Grade("g-normal", "", "", "", "", "normal", "15"),
        };

        private static readonly object[] StackInputs =
        {
            In("in_family", "Grade family", "grade_family", "string"),
            In("in_over", "Stack already at the grade limit", "over_limit", "boolean"),
            In("in_capacity", "Pit has room", "has_capacity", "boolean"),
            In("in_stackable", "Stackable pit", "stackable", "boolean"),
            In("in_used", "Pieces in the pit, including pending", "stack_used", "number"),
            In("in_grade", "Same grade as the top slab", "same_grade", "boolean"),
            In("in_size", "Same size as the top slab", "same_size", "boolean"),
            In("in_length", "Incoming length is not longer than the top", "length_ok", "boolean"),
            In("in_width", "Incoming width is not wider than the top", "width_ok", "boolean"),
        };

        private static readonly object[] StackOutputs =
        {
            Out("out_allowed", "Allowed", "allowed", "boolean"),
            Out("out_reason", "Reason", "reason", "string"),
        };

        private static readonly object[] StackRules =
        {
            Stack("s-martensitic", "== \"martensitic\"", "true", "", "", "", "", "", "", "", "false", "Martensitic and 430 Mint grades may stack at most 5 high."),
            Stack("s-400", "== \"400_series\"", "true", "", "", "", "", "", "", "", "false", "400-series grades may stack at most 10 high."),
            Stack("s-normal", "== \"normal\"", "true", "", "", "", "", "", "", "", "false", "This grade may stack at most 15 high."),
            Stack("s-limit", "", "true", "", "", "", "", "", "", "", "false", "This grade is over its stack limit."),
            Stack("s-capacity", "", "", "false", "", "", "", "", "", "", "false", "Stack is at capacity for this pit."),
            Stack("s-floor", "", "", "true", "false", "", "", "", "", "", "true", ""),
            Stack("s-empty", "", "", "true", "true", "== 0", "", "", "", "", "true", ""),
            Stack("s-grade", "", "", "true", "true", "", "false", "", "", "", "false", "Slabs of a different grade cannot share a stack."),
            Stack("s-size", "", "", "true", "true", "", "true", "false", "", "", "false", "Slabs of a different size cannot share a stack."),
            Stack("s-length", "", "", "true", "true", "", "true", "true", "false", "", "false", "Longer slabs must sit below shorter ones."),
            Stack("s-width", "", "", "true", "true", "", "true", "true", "true", "false", "false", "Wider slabs must sit below narrower ones."),
            Stack("s-allow", "", "", "", "", "", "", "", "", "", "true", ""),
        };

        private static object Node(string type, string id, string name, int x, int y, object content) => new
        {
            type,
            id,
            name,
            position = new { x, y },
            content,
        };

        private static object Table(string id, string name, int x, int y, string mode, string? inputField, string? outputPath, object[] inputs, object[] outputs, object[] rules) => new
        {
            type = "decisionTableNode",
            id,
            name,
            position = new { x, y },
            content = new
            {
                hitPolicy = "first",
                passThrough = true,
                inputField,
                outputPath,
                executionMode = mode,
                inputs,
                outputs,
                rules,
            },
        };

        private static object Edge(string id, string sourceId, string targetId) => new
        {
            id,
            sourceId,
            targetId,
            type = "edge",
        };

        private static object In(string id, string name, string field, string type) => new
        {
            id,
            name,
            field,
            fieldType = new { type },
        };

        private static object Out(string id, string name, string field, string type) => new
        {
            id,
            name,
            field,
            outputFieldType = new { type },
        };

        private static Dictionary<string, string> Grade(string id, string grade, string ph, string number, string en, string family, string max) => new()
        {
            ["_id"] = id,
            ["in_grade"] = grade,
            ["in_ph"] = ph,
            ["in_num"] = number,
            ["in_en"] = en,
            ["out_family"] = "\"" + family + "\"",
            ["out_max"] = max,
        };

        private static Dictionary<string, string> Stack(
            string id, string family, string over, string capacity, string stackable, string used,
            string grade, string size, string length, string width, string allowed, string reason) => new()
        {
            ["_id"] = id,
            ["in_family"] = family,
            ["in_over"] = over,
            ["in_capacity"] = capacity,
            ["in_stackable"] = stackable,
            ["in_used"] = used,
            ["in_grade"] = grade,
            ["in_size"] = size,
            ["in_length"] = length,
            ["in_width"] = width,
            ["out_allowed"] = allowed,
            ["out_reason"] = string.IsNullOrEmpty(reason) ? "\"\"" : "\"" + reason + "\"",
        };

        public static string For(string tableName) => $$"""
{
  "nodes": [
    {
      "type": "inputNode",
      "content": { "schema": "" },
      "id": "a705f3f8-2347-4852-b062-216d1c572173",
      "name": "request",
      "position": { "x": -520, "y": 180 }
    },
    {
      "type": "decisionTableNode",
      "content": {
        "hitPolicy": "first",
        "passThrough": true,
        "inputField": null,
        "outputPath": null,
        "executionMode": "single",
        "inputs": [
          { "id": "in_has_capacity", "name": "Has capacity (occupied < pit capacity)", "field": "has_capacity" },
          { "id": "in_capacity", "name": "Pit capacity (column limit)", "field": "capacity" },
          { "id": "in_occupied", "name": "Pieces already in pit", "field": "occupied" },
          { "id": "in_stackable", "name": "Stackable (slab / billet / plate)", "field": "stackable" },
          { "id": "in_same_grade", "name": "Same grade", "field": "same_grade" },
          { "id": "in_same_size", "name": "Same size", "field": "same_size" },
          { "id": "in_length_ok", "name": "Length fits (incoming ≤ top)", "field": "length_ok" },
          { "id": "in_width_ok", "name": "Width fits (incoming ≤ top)", "field": "width_ok" }
        ],
        "outputs": [
          { "id": "out_allowed", "name": "Allowed", "field": "allowed" },
          { "id": "out_reason", "name": "Reason", "field": "reason" }
        ],
        "rules": [
          {
            "_id": "r-at-capacity",
            "in_has_capacity": "false",
            "in_capacity": "",
            "in_occupied": "",
            "in_stackable": "",
            "in_same_grade": "",
            "in_same_size": "",
            "in_length_ok": "",
            "in_width_ok": "",
            "out_allowed": "false",
            "out_reason": "\"Stack is at capacity for this pit.\""
          },
          {
            "_id": "r-not-stackable",
            "in_has_capacity": "true",
            "in_capacity": "",
            "in_occupied": "",
            "in_stackable": "false",
            "in_same_grade": "",
            "in_same_size": "",
            "in_length_ok": "",
            "in_width_ok": "",
            "out_allowed": "true",
            "out_reason": "\"\""
          },
          {
            "_id": "r-empty-pit",
            "in_has_capacity": "true",
            "in_capacity": "",
            "in_occupied": "== 0",
            "in_stackable": "true",
            "in_same_grade": "",
            "in_same_size": "",
            "in_length_ok": "",
            "in_width_ok": "",
            "out_allowed": "true",
            "out_reason": "\"\""
          },
          {
            "_id": "r-same-grade-size-pyramid",
            "in_has_capacity": "true",
            "in_capacity": "",
            "in_occupied": "",
            "in_stackable": "true",
            "in_same_grade": "true",
            "in_same_size": "true",
            "in_length_ok": "true",
            "in_width_ok": "true",
            "out_allowed": "true",
            "out_reason": "\"\""
          },
          {
            "_id": "r-deny",
            "in_has_capacity": "",
            "in_capacity": "",
            "in_occupied": "",
            "in_stackable": "",
            "in_same_grade": "",
            "in_same_size": "",
            "in_length_ok": "",
            "in_width_ok": "",
            "out_allowed": "false",
            "out_reason": "\"Cannot stack: keep the same grade and size, stay within pit capacity, and put longer and wider pieces below.\""
          }
        ]
      },
      "id": "a65b3a7b-5e67-48c3-8760-594ed9cf2933",
      "name": "{{tableName}}",
      "position": { "x": -160, "y": 185 }
    },
    {
      "type": "outputNode",
      "content": { "schema": "" },
      "id": "365ec81f-7d58-46f6-87a1-ee199d6cb44f",
      "name": "response",
      "position": { "x": 280, "y": 180 }
    }
  ],
  "edges": [
    {
      "id": "294e3de6-b714-463d-b265-25af72c33341",
      "sourceId": "a705f3f8-2347-4852-b062-216d1c572173",
      "type": "edge",
      "targetId": "a65b3a7b-5e67-48c3-8760-594ed9cf2933"
    },
    {
      "id": "58e2fe6c-a71e-4175-82f0-db96a9c3fd66",
      "sourceId": "a65b3a7b-5e67-48c3-8760-594ed9cf2933",
      "type": "edge",
      "targetId": "365ec81f-7d58-46f6-87a1-ee199d6cb44f"
    }
  ]
}
""";
    }
}
