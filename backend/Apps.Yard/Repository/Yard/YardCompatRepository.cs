using System.Data;
using Dapper;
using Primworth_API.IRepository;
using Primworth_API.Models.Yard;
using Primworth_API.Queries.Yard;

namespace Primworth_API.Repository.Yard
{
    /// <summary>
    /// Reads for the embedded mSYMS screens. Every query here computes hot/warm/cold from the
    /// item's cast time against the facility's two thermal config values, so the screens see a
    /// clock that is always right rather than a stored state a background job has to maintain.
    /// </summary>
    public class YardCompatRepository : IYardCompatRepository
    {
        private readonly DapperContext _context;

        public YardCompatRepository(DapperContext context) => _context = context;

        public async Task<YardCompatOverview> GetOverviewAsync(string schema)
        {
            using var connection = _context.CreateConnection();
            var t = await ThermalAsync(connection, schema);
            return new YardCompatOverview
            {
                zones = (await connection.QueryAsync<YardCompatZoneRow>(Sql(YardCompatQueries.OverviewZones, schema))).ToList(),
                positions = (await connection.QueryAsync<YardCompatPositionRow>(Sql(YardCompatQueries.OverviewPositions, schema))).ToList(),
                items = (await connection.QueryAsync<YardCompatItemRow>(Sql(YardCompatQueries.OverviewItems, schema), new { t.hot, t.warm })).ToList(),
            };
        }

        public async Task<IEnumerable<YardCompatItem>> GetItemsAsync(string schema, string? qualityStatus)
        {
            using var connection = _context.CreateConnection();
            var t = await ThermalAsync(connection, schema);
            return await connection.QueryAsync<YardCompatItem>(Sql(YardCompatQueries.GetItems, schema),
                new { quality_status = string.IsNullOrWhiteSpace(qualityStatus) ? null : qualityStatus.Trim().ToUpperInvariant(), t.hot, t.warm });
        }

        public async Task<YardCompatItem?> GetItemByIdAsync(string schema, long itemId)
        {
            using var connection = _context.CreateConnection();
            var t = await ThermalAsync(connection, schema);
            return await connection.QueryFirstOrDefaultAsync<YardCompatItem>(Sql(YardCompatQueries.GetItemById, schema), new { item_id = itemId, t.hot, t.warm });
        }

        public async Task<IEnumerable<YardCompatItem>> GetDispatchCandidatesAsync(string schema)
        {
            using var connection = _context.CreateConnection();
            var t = await ThermalAsync(connection, schema);
            return await connection.QueryAsync<YardCompatItem>(Sql(YardCompatQueries.GetDispatchCandidates, schema), new { t.hot, t.warm });
        }

        public async Task<IEnumerable<YardCompatItem>> GetMillCampaignAsync(string schema, int limit)
        {
            using var connection = _context.CreateConnection();
            var t = await ThermalAsync(connection, schema);
            return await connection.QueryAsync<YardCompatItem>(Sql(YardCompatQueries.GetMillCampaign, schema),
                new { limit = limit is < 1 or > 500 ? 100 : limit, t.hot, t.warm });
        }

        public async Task<IEnumerable<YardCompatWorkOrderRow>> GetWorkOrdersAsync(string schema)
        {
            using var connection = _context.CreateConnection();
            var t = await ThermalAsync(connection, schema);
            return await connection.QueryAsync<YardCompatWorkOrderRow>(Sql(YardCompatQueries.GetWorkOrders, schema), new { t.hot, t.warm });
        }

        /// <summary>The two thermal bands, from config, with the seeded defaults as fallback.</summary>
        private static async Task<(int hot, int warm)> ThermalAsync(IDbConnection connection, string schema)
        {
            var rows = await connection.QueryAsync<(string config_key, string config_value)>(Sql(YardZoneQueries.GetConfig, schema));
            var config = rows.ToDictionary(r => r.config_key, r => r.config_value ?? "", StringComparer.OrdinalIgnoreCase);
            var hot = Minutes(config, "thermal_hot_minutes", 2);
            var warm = Math.Max(Minutes(config, "thermal_warm_minutes", 4), hot);
            return (hot, warm);
        }

        private static int Minutes(IReadOnlyDictionary<string, string> config, string key, int fallback)
            => config.TryGetValue(key, out var v) && int.TryParse(v, out var n) && n > 0 ? n : fallback;

        private static string Sql(string sql, string schema) => sql.Replace("{schema}", schema);
    }
}
