using Dapper;
using Npgsql;
using Primworth_API.IRepository;
using Primworth_API.Models.Yard;
using Primworth_API.Queries.Yard;

namespace Primworth_API.Repository.Yard
{
    public class YardMasterRepository : IYardMasterRepository
    {
        private const string UniqueViolation = "23505";
        private readonly DapperContext _context;

        public YardMasterRepository(DapperContext context) => _context = context;

        // ── Grades: the platform's gm_grade, never a copy ────────────────────────
        public async Task<IEnumerable<YardGradeOption>> GetGradesAsync(string schema)
        {
            using var connection = _context.CreateConnection();
            var exists = await connection.ExecuteScalarAsync<int>(YardMasterQueries.GradeTableExists, new { schemaName = schema });
            if (exists == 0)
                throw new YardRuleException("Grade master (gm_grade) is not available in this facility. Map it to the Yard Management application in Application Master.");
            return await connection.QueryAsync<YardGradeOption>(Sql(YardMasterQueries.GetGrades, schema));
        }

        // ── Size profiles ────────────────────────────────────────────────────────
        public async Task<IEnumerable<YardSizeProfile>> GetSizeProfilesAsync(string schema)
        {
            using var connection = _context.CreateConnection();
            return await connection.QueryAsync<YardSizeProfile>(Sql(YardMasterQueries.GetSizeProfiles, schema));
        }

        public async Task<long> SaveSizeProfileAsync(string schema, long? id, YardSizeProfileInput input, long userId)
        {
            using var connection = _context.CreateConnection();
            var p = new
            {
                id, profile_name = input.profile_name.Trim(), input.length_mm, input.width_mm, input.thickness_mm,
                input.tolerance_pct, input.is_active, user_id = User(userId),
            };
            try
            {
                if (id is null) return await connection.ExecuteScalarAsync<long>(Sql(YardMasterQueries.InsertSizeProfile, schema), p);
                var affected = await connection.ExecuteAsync(Sql(YardMasterQueries.UpdateSizeProfile, schema), p);
                if (affected == 0) throw new YardRuleException("That size profile no longer exists.");
                return id.Value;
            }
            catch (PostgresException ex) when (ex.SqlState == UniqueViolation)
            { throw new YardDuplicateException($"A size profile named '{input.profile_name.Trim()}' already exists."); }
        }

        public async Task<bool> DeleteSizeProfileAsync(string schema, long id, long userId)
        {
            using var connection = _context.CreateConnection();
            // Refused rather than cascaded: a profile still attached to items is what routing matches on.
            var uses = await connection.ExecuteScalarAsync<long>(Sql(YardMasterQueries.CountSizeProfileUses, schema), new { id });
            if (uses > 0) throw new YardRuleException($"That profile is still used by {uses} item(s) or product mix rule(s), so it cannot be removed.");
            return await connection.ExecuteAsync(Sql(YardMasterQueries.SoftDeleteSizeProfile, schema), new { id, user_id = User(userId) }) > 0;
        }

        // ── Rework activities ────────────────────────────────────────────────────
        public async Task<IEnumerable<YardReworkActivity>> GetReworkActivitiesAsync(string schema)
        {
            using var connection = _context.CreateConnection();
            return await connection.QueryAsync<YardReworkActivity>(Sql(YardMasterQueries.GetReworkActivities, schema));
        }

        public async Task<long> SaveReworkActivityAsync(string schema, long? id, YardReworkActivityInput input, long userId)
        {
            using var connection = _context.CreateConnection();
            var p = new
            {
                id, activity_name = input.activity_name.Trim(),
                description = string.IsNullOrWhiteSpace(input.description) ? null : input.description.Trim(),
                input.is_active, user_id = User(userId),
            };
            try
            {
                if (id is null) return await connection.ExecuteScalarAsync<long>(Sql(YardMasterQueries.InsertReworkActivity, schema), p);
                var affected = await connection.ExecuteAsync(Sql(YardMasterQueries.UpdateReworkActivity, schema), p);
                if (affected == 0) throw new YardRuleException("That rework activity no longer exists.");
                return id.Value;
            }
            catch (PostgresException ex) when (ex.SqlState == UniqueViolation)
            { throw new YardDuplicateException($"A rework activity named '{input.activity_name.Trim()}' already exists."); }
        }

        public async Task<bool> DeleteReworkActivityAsync(string schema, long id, long userId)
        {
            using var connection = _context.CreateConnection();
            return await connection.ExecuteAsync(Sql(YardMasterQueries.SoftDeleteReworkActivity, schema), new { id, user_id = User(userId) }) > 0;
        }

        // ── Product mixes ────────────────────────────────────────────────────────
        public async Task<IEnumerable<YardProductMix>> GetProductMixesAsync(string schema)
        {
            using var connection = _context.CreateConnection();
            return await connection.QueryAsync<YardProductMix>(Sql(YardMasterQueries.GetProductMixes, schema));
        }

        /// <summary>Parent and children in one transaction; children replaced wholesale, the sets are small.</summary>
        public async Task<long> SaveProductMixAsync(string schema, long id, YardProductMixInput input, long userId)
        {
            using var connection = _context.CreateConnection();
            connection.Open();
            using var tx = connection.BeginTransaction();

            var clash = await connection.ExecuteScalarAsync<int>(new CommandDefinition(Sql(YardMasterQueries.CountMixByName, schema),
                new { mix_name = input.mix_name, product_mix_id = id }, tx));
            if (clash > 0) throw new YardDuplicateException($"A product mix named '{input.mix_name.Trim()}' already exists.");

            var p = new
            {
                product_mix_id = id, mix_name = input.mix_name.Trim(), input.description, input.material_type_id,
                target_zone_purpose = input.target_zone_purpose.Trim().ToUpperInvariant(), input.priority, input.is_active, user_id = User(userId),
            };

            long mixId;
            if (id > 0)
            {
                var affected = await connection.ExecuteAsync(new CommandDefinition(Sql(YardMasterQueries.UpdateProductMix, schema), p, tx));
                if (affected == 0) throw new YardRuleException("Product mix not found.");
                mixId = id;
            }
            else
            {
                mixId = await connection.ExecuteScalarAsync<long>(new CommandDefinition(Sql(YardMasterQueries.InsertProductMix, schema), p, tx));
            }

            await connection.ExecuteAsync(new CommandDefinition(Sql(YardMasterQueries.DeleteMixGrades, schema), new { product_mix_id = mixId }, tx));
            foreach (var grade in input.allowed_grades.Where(g => !string.IsNullOrWhiteSpace(g)).Select(g => g.Trim()).Distinct(StringComparer.OrdinalIgnoreCase))
                await connection.ExecuteAsync(new CommandDefinition(Sql(YardMasterQueries.InsertMixGrade, schema), new { product_mix_id = mixId, grade_code = grade }, tx));

            await connection.ExecuteAsync(new CommandDefinition(Sql(YardMasterQueries.DeleteMixSizes, schema), new { product_mix_id = mixId }, tx));
            foreach (var sizeId in input.allowed_size_profile_ids.Where(s => s > 0).Distinct())
                await connection.ExecuteAsync(new CommandDefinition(Sql(YardMasterQueries.InsertMixSize, schema), new { product_mix_id = mixId, size_profile_id = sizeId }, tx));

            tx.Commit();
            return mixId;
        }

        public async Task<bool> DeleteProductMixAsync(string schema, long id, long userId)
        {
            using var connection = _context.CreateConnection();
            return await connection.ExecuteAsync(Sql(YardMasterQueries.SoftDeleteProductMix, schema), new { product_mix_id = id, user_id = User(userId) }) > 0;
        }

        // ── Activity log ─────────────────────────────────────────────────────────
        public async Task<(IEnumerable<YardSystemLogRow> rows, long total)> GetSystemLogsAsync(string schema, int page, int limit, string? action, DateTime? from, DateTime? to)
        {
            var take = limit is < 1 or > 500 ? 50 : limit;
            var filters = new
            {
                action = string.IsNullOrWhiteSpace(action) ? null : action.Trim(), from, to,
                limit = take, offset = (Math.Max(page, 1) - 1) * take,
            };
            using var connection = _context.CreateConnection();
            var rows = await connection.QueryAsync<YardSystemLogRow>(Sql(YardMasterQueries.GetSystemLogs, schema), filters);
            var total = await connection.ExecuteScalarAsync<long>(Sql(YardMasterQueries.CountSystemLogs, schema), filters);
            return (rows, total);
        }

        private static string Sql(string sql, string schema) => sql.Replace("{schema}", schema);
        private static long? User(long userId) => userId == 0 ? null : userId;
    }
}
