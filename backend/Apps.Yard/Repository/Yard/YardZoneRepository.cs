using Dapper;
using Npgsql;
using Primworth_API.IRepository;
using Primworth_API.Models.Yard;
using Primworth_API.Queries.Yard;

namespace Primworth_API.Repository.Yard
{
    public class YardZoneRepository : IYardZoneRepository
    {
        private const string UniqueViolation = "23505";
        private readonly DapperContext _context;

        public YardZoneRepository(DapperContext context) => _context = context;

        public async Task<IEnumerable<YardMaterialType>> GetMaterialTypesAsync(string schema, bool includeInactive)
        {
            using var connection = _context.CreateConnection();
            return await connection.QueryAsync<YardMaterialType>(Sql(YardZoneQueries.GetMaterialTypes, schema), new { include_inactive = includeInactive });
        }

        public async Task<IEnumerable<YardZone>> GetZonesAsync(string schema)
        {
            using var connection = _context.CreateConnection();
            return await connection.QueryAsync<YardZone>(Sql(YardZoneQueries.GetZones, schema));
        }

        public async Task<IEnumerable<YardPosition>> GetPositionsAsync(string schema, long zoneId)
        {
            using var connection = _context.CreateConnection();
            return await connection.QueryAsync<YardPosition>(Sql(YardZoneQueries.GetPositionsByZone, schema), new { zone_id = zoneId });
        }

        /// <summary>
        /// Area, accepted material types and the position grid in one transaction: an area with no
        /// positions is not a usable part of the yard, so the three land together or not at all.
        /// </summary>
        public async Task<long> CreateZoneAsync(string schema, YardZoneInput input, long userId)
        {
            var style = input.storage_style.Trim().ToUpperInvariant();

            // RACK: every slot holds exactly one item. FLOOR: one position holds the whole area.
            var capacity = style == YardStorageStyle.Rack ? 1 : input.capacity_limit;
            var rows = style == YardStorageStyle.Floor ? 1 : input.rows;
            var cols = style == YardStorageStyle.Floor ? 1 : input.cols;

            using var connection = _context.CreateConnection();
            connection.Open();
            using var tx = connection.BeginTransaction();

            var clash = await connection.ExecuteScalarAsync<int>(new CommandDefinition(
                Sql(YardZoneQueries.CountActiveByName, schema), new { zone_name = input.zone_name }, tx));
            if (clash > 0) throw new YardDuplicateException($"An area named '{input.zone_name.Trim()}' already exists in this yard.");

            long zoneId;
            try
            {
                zoneId = await connection.ExecuteScalarAsync<long>(new CommandDefinition(Sql(YardZoneQueries.InsertZone, schema), new
                {
                    zone_name = input.zone_name.Trim(),
                    zone_purpose = input.zone_purpose.Trim().ToUpperInvariant(),
                    storage_style = style,
                    user_id = User(userId),
                }, tx));
            }
            catch (PostgresException ex) when (ex.SqlState == UniqueViolation)
            { throw new YardDuplicateException($"An area named '{input.zone_name.Trim()}' already exists in this yard."); }

            var accepted = 0;
            foreach (var key in input.material_type_keys.Where(k => !string.IsNullOrWhiteSpace(k)).Distinct(StringComparer.OrdinalIgnoreCase))
            {
                accepted += await connection.ExecuteAsync(new CommandDefinition(
                    Sql(YardZoneQueries.InsertZoneMaterialType, schema), new { zone_id = zoneId, type_key = key.Trim() }, tx));
            }
            if (accepted == 0)
                throw new YardRuleException("None of the material type keys given exists in this facility. Check the Material Types master.");

            await connection.ExecuteAsync(new CommandDefinition(Sql(YardZoneQueries.InsertPositionGrid, schema), new
            {
                zone_id = zoneId, rows, cols, capacity_limit = capacity, user_id = User(userId),
            }, tx));

            tx.Commit();
            return zoneId;
        }

        public async Task<bool> UpdatePositionCapacityAsync(string schema, long positionId, int capacityLimit, long userId)
        {
            using var connection = _context.CreateConnection();

            // The yard is the source of truth: capacity cannot go below what is physically there.
            var occupied = await connection.ExecuteScalarAsync<int>(Sql(YardZoneQueries.CountItemsInPosition, schema), new { position_id = positionId });
            if (capacityLimit < occupied)
                throw new YardRuleException($"Capacity cannot be set below {occupied}: that many items are in this position now.");

            return await connection.ExecuteAsync(Sql(YardZoneQueries.UpdatePositionCapacity, schema),
                new { position_id = positionId, capacity_limit = capacityLimit, user_id = User(userId) }) > 0;
        }

        public async Task<bool> DeleteZoneAsync(string schema, long zoneId, long userId)
        {
            using var connection = _context.CreateConnection();

            var count = await connection.ExecuteScalarAsync<int>(Sql(YardZoneQueries.CountItemsInZone, schema), new { zone_id = zoneId });
            if (count > 0) throw new YardRuleException($"This area still holds {count} item(s). Move or dispatch them before deleting it.");

            return await connection.ExecuteAsync(Sql(YardZoneQueries.SoftDeleteZone, schema), new { zone_id = zoneId, user_id = User(userId) }) > 0;
        }

        public async Task<IReadOnlyDictionary<string, string>> GetConfigAsync(string schema)
        {
            using var connection = _context.CreateConnection();
            var rows = await connection.QueryAsync<(string config_key, string config_value)>(Sql(YardZoneQueries.GetConfig, schema));
            return rows.ToDictionary(r => r.config_key, r => r.config_value ?? "", StringComparer.OrdinalIgnoreCase);
        }

        public async Task SetConfigAsync(string schema, string key, string? value, long userId)
        {
            using var connection = _context.CreateConnection();
            await connection.ExecuteAsync(Sql(YardZoneQueries.UpsertConfig, schema), new { config_key = key, config_value = value, user_id = User(userId) });
        }

        private static string Sql(string sql, string schema) => sql.Replace("{schema}", schema);
        private static long? User(long userId) => userId == 0 ? null : userId;
    }
}
