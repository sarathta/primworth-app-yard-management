using System.Text.Json;
using Dapper;
using Npgsql;
using Primworth_API.IRepository;
using Primworth_API.Models.Yard;
using Primworth_API.Queries.Yard;

namespace Primworth_API.Repository.Yard
{
    public class YardItemRepository : IYardItemRepository
    {
        private const string UniqueViolation = "23505";
        private readonly DapperContext _context;
        private readonly IYardRuleEngine _rules;
        private readonly IYardRulesRepository _yardRules;

        public YardItemRepository(DapperContext context, IYardRuleEngine rules, IYardRulesRepository yardRules)
        {
            _context = context;
            _rules = rules;
            _yardRules = yardRules;
        }

        public async Task<IEnumerable<YardItem>> GetItemsAsync(string schema, YardItemQuery query)
        {
            var page = query.page < 1 ? 1 : query.page;
            var pageSize = query.pageSize is < 1 or > 1000 ? 100 : query.pageSize;

            using var connection = _context.CreateConnection();
            return await connection.QueryAsync<YardItem>(Sql(YardItemQueries.GetItems, schema), new
            {
                item_status = Blank(query.itemStatus),
                quality_status = Blank(query.qualityStatus),
                zone_id = query.zoneId,
                search = (query.search ?? "").Trim().ToLowerInvariant(),
                page_size = pageSize,
                offset = (page - 1) * pageSize,
            });
        }

        public async Task<YardItem?> GetItemByIdAsync(string schema, long itemId)
        {
            using var connection = _context.CreateConnection();
            return await connection.QueryFirstOrDefaultAsync<YardItem>(Sql(YardItemQueries.GetItemById, schema), new { item_id = itemId });
        }

        /// <summary>
        /// Ingest and allocate in one transaction. The attributes are checked against the material
        /// type's definitions before anything is written, so a bad feed message is refused in words
        /// rather than stored and discovered later by a report.
        /// </summary>
        public async Task<YardAllocationResult> CreateItemAsync(string schema, YardItemInput input, long userId, long? yardTypeId = null, bool requireAllocated = false)
        {
            using var connection = _context.CreateConnection();
            connection.Open();
            using var tx = connection.BeginTransaction();

            var type = await ResolveTypeAsync(connection, tx, schema, input.material_type_key, yardTypeId);
            var itemCode = Blank(input.item_code);
            var heatNumber = Blank(input.heat_number);
            var gradeCode = string.IsNullOrWhiteSpace(input.grade_code) ? null : input.grade_code.Trim();
            var lengthMm = input.length_mm;
            var widthMm = input.width_mm;
            var thicknessMm = input.thickness_mm;
            if (input.size_profile_id is > 0)
            {
                var profile = await connection.QueryFirstOrDefaultAsync<YardSizeProfile>(new CommandDefinition(
                    Sql(YardItemQueries.GetSizeProfileById, schema), new { size_profile_id = input.size_profile_id }, tx))
                    ?? throw new YardRuleException("That size profile is not in this facility.");
                lengthMm ??= profile.length_mm;
                widthMm ??= profile.width_mm;
                thicknessMm ??= profile.thickness_mm;
            }

            var defs = YardAttributes.Parse(type.attribute_defs);
            var attributes = MergeDimensions(input.attributes, defs, lengthMm, widthMm, thicknessMm);
            var problem = YardAttributes.Validate(attributes, defs, out var attributesJson);
            if (problem != null) throw new YardRuleException($"{type.singular} '{itemCode ?? "new"}': {problem}");

            lengthMm = YardAttributes.Number(attributesJson, "length_mm") ?? lengthMm;
            widthMm = YardAttributes.Number(attributesJson, "width_mm") ?? widthMm;
            if (YardRuleEngine.IsStackable(type.type_key) && (lengthMm is null or <= 0 || widthMm is null or <= 0))
                throw new YardRuleException("Length and width are required in attributes so this piece can be stacked (longer and wider below).");

            var generatedItem = itemCode == null;
            var itemSerial = 0;
            var itemPrefix = ItemPrefix(type.type_key);

            if (itemCode == null || heatNumber == null)
            {
                var serials = await connection.QueryFirstAsync<(int last_item, int last_heat)>(new CommandDefinition(
                    Sql(YardItemQueries.GetNextInboundSerials, schema), new { item_prefix = itemPrefix }, tx));
                if (itemCode == null)
                {
                    itemSerial = serials.last_item + 1;
                    itemCode = FormatItemCode(itemPrefix, itemSerial);
                }
                heatNumber ??= FormatHeatNumber(serials.last_heat + 1);
            }

            if (string.Equals(itemCode, heatNumber, StringComparison.OrdinalIgnoreCase))
                heatNumber = FormatHeatNumber(1);

            long itemId = 0;
            for (var attempt = 0; ; attempt++)
            {
                try
                {
                    itemId = await connection.ExecuteScalarAsync<long>(new CommandDefinition(Sql(YardItemQueries.InsertItem, schema), new
                    {
                        item_code = itemCode,
                        heat_number = heatNumber,
                        material_type_id = type.material_type_id,
                        grade_code = gradeCode,
                        weight_kg = input.weight_kg,
                        attributes = attributesJson,
                        size_profile_id = input.size_profile_id,
                        user_id = User(userId),
                    }, tx));
                    break;
                }
                catch (PostgresException ex) when (ex.SqlState == UniqueViolation && generatedItem && attempt < 7)
                {
                    itemSerial++;
                    itemCode = FormatItemCode(itemPrefix, itemSerial);
                    if (string.Equals(itemCode, heatNumber, StringComparison.OrdinalIgnoreCase))
                        heatNumber = FormatHeatNumber(itemSerial + 1);
                }
                catch (PostgresException ex) when (ex.SqlState == UniqueViolation)
                {
                    throw new YardDuplicateException($"{type.singular} '{itemCode}' already exists in this yard.");
                }
            }

            var result = new YardAllocationResult { item_id = itemId, item_code = itemCode, heat_number = heatNumber, grade_code = gradeCode };

            if (input.allocate)
            {
                var item = await connection.QueryFirstOrDefaultAsync<YardItem>(new CommandDefinition(
                    Sql(YardItemQueries.GetItemById, schema), new { item_id = itemId }, tx));
                var config = (await connection.QueryAsync<(string config_key, string config_value)>(new CommandDefinition(
                    Sql(YardZoneQueries.GetConfig, schema), transaction: tx)))
                    .ToDictionary(r => r.config_key, r => r.config_value ?? "", StringComparer.OrdinalIgnoreCase);
                var graphJson = yardTypeId is > 0
                    ? await _yardRules.GetGraphAsync(connection, tx, schema, yardTypeId.Value)
                    : null;

                result = await YardPlacement.AllocateInboundAsync(new YardPlacement.Context
                {
                    Connection = connection, Transaction = tx, Schema = schema, UserId = userId,
                    YardTypeId = yardTypeId is > 0 ? yardTypeId : null, Config = config, Rules = _rules,
                    RuleGraphJson = graphJson,
                }, item!);
                result.item_code = itemCode;
                result.heat_number = heatNumber;
                result.grade_code = gradeCode;
                if (requireAllocated && !result.allocated)
                    throw new YardRuleException(result.reason ?? "There is no free slot on this yard.");
            }

            tx.Commit();
            return result;
        }

        public async Task<bool> SetQualityAsync(string schema, long itemId, string qualityStatus, long userId)
        {
            using var connection = _context.CreateConnection();
            return await connection.ExecuteAsync(Sql(YardItemQueries.SetQualityStatus, schema),
                new { item_id = itemId, quality_status = qualityStatus, user_id = User(userId) }) > 0;
        }

        /// <summary>
        /// Takes an item out of the yard (ship or scrap) and closes the gap it leaves. A held item
        /// cannot leave by any route.
        /// </summary>
        public async Task<bool> RemoveFromYardAsync(string schema, long itemId, string newStatus, string orderType, long userId)
        {
            using var connection = _context.CreateConnection();
            connection.Open();
            using var tx = connection.BeginTransaction();

            var item = await connection.QueryFirstOrDefaultAsync<YardItem>(new CommandDefinition(
                Sql(YardItemQueries.GetItemById, schema), new { item_id = itemId }, tx))
                ?? throw new YardRuleException("Item not found.");

            if (item.quality_status == YardQualityStatus.LockedQa)
                throw new YardRuleException($"QA HOLD: item '{item.item_code}' is under review and cannot be removed.");

            await connection.ExecuteAsync(new CommandDefinition(Sql(YardItemQueries.ClearItemLocation, schema),
                new { item_id = item.item_id, item_status = newStatus, user_id = User(userId) }, tx));

            if (item.position_id.HasValue && item.stack_index.HasValue)
            {
                await connection.ExecuteAsync(new CommandDefinition(Sql(YardItemQueries.RepackAbove, schema),
                    new { position_id = item.position_id.Value, removed_stack_index = item.stack_index.Value, user_id = User(userId) }, tx));
            }

            await YardMovementLog.WriteMovementAsync(connection, tx, schema, userId, item.item_id, item.zone_id, null,
                item.zone_name, null, item.PositionLabel, null, orderType);

            tx.Commit();
            return true;
        }

        /// <summary>
        /// The material type named in the request, else the one this yard's STORAGE areas accept,
        /// else the facility's only active type. A slab yard should never have to say SLAB.
        /// </summary>
        private static async Task<YardMaterialType> ResolveTypeAsync(System.Data.IDbConnection connection, System.Data.IDbTransaction tx, string schema, string? key, long? yardTypeId)
        {
            if (!string.IsNullOrWhiteSpace(key))
            {
                return await connection.QueryFirstOrDefaultAsync<YardMaterialType>(new CommandDefinition(
                           Sql(YardZoneQueries.GetMaterialTypeByKey, schema), new { type_key = key.Trim() }, tx))
                       ?? throw new YardRuleException($"Material type '{key.Trim()}' is not configured in this facility.");
            }

            if (yardTypeId is > 0)
            {
                var onYard = (await connection.QueryAsync<YardMaterialType>(new CommandDefinition(
                    Sql(YardZoneQueries.GetActiveMaterialTypesForYard, schema), new { yard_type_id = yardTypeId }, tx))).ToList();
                if (onYard.Count == 1) return onYard[0];
                if (onYard.Count == 0)
                    throw new YardRuleException("This yard has no active storage area that accepts a material.");
                throw new YardRuleException("This yard stores more than one material; say which one (material_type_key).");
            }

            var active = (await connection.QueryAsync<YardMaterialType>(new CommandDefinition(
                Sql(YardZoneQueries.GetOnlyActiveMaterialType, schema), transaction: tx))).ToList();

            return active.Count switch
            {
                1 => active[0],
                0 => throw new YardRuleException("No active material type is configured in this facility."),
                _ => throw new YardRuleException("This facility stores more than one material; say which one (material_type_key)."),
            };
        }

        private static string Sql(string sql, string schema) => sql.Replace("{schema}", schema);
        private static string? Blank(string? v) => string.IsNullOrWhiteSpace(v) ? null : v.Trim().ToUpperInvariant();
        private static long? User(long userId) => userId == 0 ? null : userId;

        /// <summary>SL-4111, BD-4409: two letters from the material, matching the codes already in the yard.</summary>
        private static string ItemPrefix(string typeKey)
        {
            var key = (typeKey ?? "").Trim().ToUpperInvariant();
            return key switch
            {
                "SLAB" => "SL",
                "BILLET" => "BD",
                "COIL" => "CL",
                "TMT_BUNDLE" => "BD",
                "WIRE_ROD" => "WR",
                "PLATE" => "PL",
                _ => Letters(key),
            };
        }

        private static string Letters(string key)
        {
            var letters = new string(key.Where(char.IsLetter).Take(2).ToArray());
            return letters.Length == 2 ? letters : "IT";
        }

        private static string FormatItemCode(string prefix, int serial) => $"{prefix}-{serial.ToString().PadLeft(4, '0')}";
        private static string FormatHeatNumber(int serial) => $"H-{serial.ToString().PadLeft(5, '0')}";

        private static JsonElement? MergeDimensions(JsonElement? attributes, IReadOnlyList<YardAttributeDef> defs,
            decimal? lengthMm, decimal? widthMm, decimal? thicknessMm)
        {
            var known = new HashSet<string>(defs.Select(d => d.Key), StringComparer.OrdinalIgnoreCase);
            var seen = new HashSet<string>(StringComparer.OrdinalIgnoreCase);
            using var stream = new MemoryStream();
            using (var writer = new Utf8JsonWriter(stream))
            {
                writer.WriteStartObject();
                if (attributes is { ValueKind: JsonValueKind.Object } obj)
                {
                    foreach (var p in obj.EnumerateObject())
                    {
                        if (!seen.Add(p.Name)) continue;
                        p.WriteTo(writer);
                    }
                }
                WriteDim(writer, known, seen, "length_mm", lengthMm);
                WriteDim(writer, known, seen, "width_mm", widthMm);
                WriteDim(writer, known, seen, "thickness_mm", thicknessMm);
                writer.WriteEndObject();
            }
            using var doc = JsonDocument.Parse(stream.ToArray());
            return doc.RootElement.Clone();
        }

        private static void WriteDim(Utf8JsonWriter writer, HashSet<string> known, HashSet<string> seen, string key, decimal? value)
        {
            if (value is null || !known.Contains(key) || !seen.Add(key)) return;
            writer.WriteNumber(key, value.Value);
        }
    }
}
