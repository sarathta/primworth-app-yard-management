using System.Data;
using Dapper;
using Primworth_API.IRepository;
using Primworth_API.Queries.Yard;

namespace Primworth_API.Repository.Yard
{
    public class YardRulesRepository : IYardRulesRepository
    {
        private readonly DapperContext _context;

        public YardRulesRepository(DapperContext context) => _context = context;

        public async Task<string?> GetGraphAsync(string schema, long yardTypeId)
        {
            using var connection = _context.CreateConnection();
            return await connection.QueryFirstOrDefaultAsync<string>(Sql(YardRuleQueries.GetGraph, schema), new { yard_type_id = yardTypeId });
        }

        public Task<string?> GetGraphAsync(IDbConnection connection, IDbTransaction transaction, string schema, long yardTypeId)
            => connection.QueryFirstOrDefaultAsync<string>(new CommandDefinition(
                Sql(YardRuleQueries.GetGraph, schema), new { yard_type_id = yardTypeId }, transaction));

        public async Task UpsertGraphAsync(string schema, long yardTypeId, string graphJson, long userId)
        {
            using var connection = _context.CreateConnection();
            var exists = await connection.ExecuteScalarAsync<int>(Sql(YardRuleQueries.YardTypeExists, schema), new { yard_type_id = yardTypeId });
            if (exists == 0) throw new Models.Yard.YardRuleException("That yard is not in this facility.");
            await connection.ExecuteAsync(Sql(YardRuleQueries.UpsertGraph, schema), new
            {
                yard_type_id = yardTypeId,
                graph = graphJson,
                user_id = userId == 0 ? (long?)null : userId,
            });
        }

        private static string Sql(string sql, string schema) => sql.Replace("{schema}", schema);
    }
}
