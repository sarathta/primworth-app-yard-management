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

        public YardItemRepository(DapperContext context) => _context = context;

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
        public async Task<YardAllocationResult> CreateItemAsync(string schema, YardItemInput input, long userId)
        {
            using var connection = _context.CreateConnection();
            connection.Open();
            using var tx = connection.BeginTransaction();

            var type = await ResolveTypeAsync(connection, tx, schema, input.material_type_key);
            var problem = YardAttributes.Validate(input.attributes, YardAttributes.Parse(type.attribute_defs), out var attributesJson);
            if (problem != null) throw new YardRuleException($"{type.singular} '{input.item_code.Trim()}': {problem}");

            long itemId;
            try
            {
                itemId = await connection.ExecuteScalarAsync<long>(new CommandDefinition(Sql(YardItemQueries.InsertItem, schema), new
                {
                    item_code = input.item_code.Trim(),
                    heat_number = string.IsNullOrWhiteSpace(input.heat_number) ? null : input.heat_number.Trim(),
                    material_type_id = type.material_type_id,
                    grade_code = string.IsNullOrWhiteSpace(input.grade_code) ? null : input.grade_code.Trim(),
                    weight_kg = input.weight_kg,
                    attributes = attributesJson,
                    size_profile_id = input.size_profile_id,
                    user_id = User(userId),
                }, tx));
            }
            catch (PostgresException ex) when (ex.SqlState == UniqueViolation)
            { throw new YardDuplicateException($"{type.singular} '{input.item_code.Trim()}' already exists in this yard."); }

            var result = new YardAllocationResult { item_id = itemId };

            if (input.allocate)
            {
                var item = await connection.QueryFirstOrDefaultAsync<YardItem>(new CommandDefinition(
                    Sql(YardItemQueries.GetItemById, schema), new { item_id = itemId }, tx));
                var config = (await connection.QueryAsync<(string config_key, string config_value)>(new CommandDefinition(
                    Sql(YardZoneQueries.GetConfig, schema), transaction: tx)))
                    .ToDictionary(r => r.config_key, r => r.config_value ?? "", StringComparer.OrdinalIgnoreCase);

                result = await YardPlacement.AllocateInboundAsync(new YardPlacement.Context
                {
                    Connection = connection, Transaction = tx, Schema = schema, UserId = userId, Config = config,
                }, item!);
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
        /// The material type named in the request, or the facility's only active one when none is
        /// named: a single-material plant should never have to say the word.
        /// </summary>
        private static async Task<YardMaterialType> ResolveTypeAsync(System.Data.IDbConnection connection, System.Data.IDbTransaction tx, string schema, string? key)
        {
            if (!string.IsNullOrWhiteSpace(key))
            {
                return await connection.QueryFirstOrDefaultAsync<YardMaterialType>(new CommandDefinition(
                           Sql(YardZoneQueries.GetMaterialTypeByKey, schema), new { type_key = key.Trim() }, tx))
                       ?? throw new YardRuleException($"Material type '{key.Trim()}' is not configured in this facility.");
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
    }
}
