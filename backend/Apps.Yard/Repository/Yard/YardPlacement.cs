using System.Data;
using Dapper;
using Primworth_API.Models.Yard;
using Primworth_API.Queries.Yard;

namespace Primworth_API.Repository.Yard
{
    /// <summary>
    /// The placement rule: where an arriving item goes, and how a buried one is reached.
    ///
    /// Built in, in C#, and deliberately small. The FRD's editable decision graph slots in here:
    /// when a facility turns the graph on, Choose hands each candidate to the graph and keeps the
    /// best allowed one; the candidate list, the reservations and the work orders stay exactly as
    /// they are. Everything runs inside the caller's transaction, so an item never exists without
    /// either a work order or a reason.
    ///
    /// Storage style matters in one place: retrieval. A STACK or PYRAMID digs; a RACK or FLOOR does
    /// not. Placement itself is the same for every style because MAX(stack_index)+1 on a
    /// capacity-1 slot is 1, and on a floor the index is merely a unique number.
    /// </summary>
    public static class YardPlacement
    {
        private const string DefaultGrade = "Standard";

        public sealed class Context
        {
            public required IDbConnection Connection { get; init; }
            public required IDbTransaction Transaction { get; init; }
            public required string Schema { get; init; }
            public long UserId { get; init; }
            public IReadOnlyDictionary<string, string> Config { get; init; } = new Dictionary<string, string>();
        }

        /// <summary>Places a PENDING item and raises its INBOUND order, or holds it with a reason.</summary>
        public static async Task<YardAllocationResult> AllocateInboundAsync(Context ctx, YardItem item)
        {
            var result = new YardAllocationResult { item_id = item.item_id };

            // Product-mix routing decides which area purpose the item belongs in. Off: STORAGE.
            var purpose = YardZonePurpose.Storage;
            if (IsOn(ctx.Config, "MODULE_MASTER_ROUTING"))
            {
                var mix = await MatchProductMixAsync(ctx, item);
                if (mix == null)
                {
                    await HoldAsync(ctx, item.item_id);
                    result.held = true;
                    result.reason = $"No product mix matches grade '{GradeOf(item)}'"
                        + (item.size_profile_id.HasValue ? $" / size profile {item.size_profile_id}" : "")
                        + ". Assign it manually or add a matching mix.";
                    return result;
                }
                purpose = mix.target_zone_purpose;
            }

            var candidates = await CandidatesAsync(ctx, item.material_type_id, purpose);
            var target = Choose(candidates, item, IsOn(ctx.Config, "rule_grade_separation"), out var rejection);

            if (target == null)
            {
                // HOLD and say why. Leaving it PENDING is indistinguishable from "not processed yet".
                await HoldAsync(ctx, item.item_id);
                result.held = true;
                result.reason = rejection;
                return result;
            }

            result.work_order_id = await RaiseOrderAsync(ctx, YardOrderType.Inbound, item.item_id, null, null, target.zone_id, target.position_id, null);
            result.allocated = true;
            result.destination = $"{target.zone_name} {target.PositionLabel}";
            return result;
        }

        /// <summary>First active mix, highest priority first, whose grade list and size list accept the item. Empty list = any.</summary>
        private static async Task<YardProductMix?> MatchProductMixAsync(Context ctx, YardItem item)
        {
            var mixes = await ctx.Connection.QueryAsync<YardProductMix>(new CommandDefinition(
                Sql(YardMasterQueries.GetActiveMixesForRouting, ctx.Schema), transaction: ctx.Transaction));
            var grade = GradeOf(item);

            foreach (var mix in mixes)
            {
                if (mix.material_type_id.HasValue && mix.material_type_id != item.material_type_id) continue;
                var grades = mix.AllowedGrades;
                var sizes = mix.AllowedSizeIds;
                var gradeOk = grades.Count == 0 || grades.Any(g => string.Equals(g, grade, StringComparison.OrdinalIgnoreCase));
                var sizeOk = sizes.Count == 0 || (item.size_profile_id.HasValue && sizes.Contains(item.size_profile_id.Value));
                if (gradeOk && sizeOk) return mix;
            }
            return null;
        }

        /// <summary>
        /// Enough room, grade-compatible when separation is on, and among those the emptiest.
        /// Candidates arrive sorted by free space, so the first survivor wins.
        /// </summary>
        private static YardCandidatePosition? Choose(List<YardCandidatePosition> candidates, YardItem item, bool strictGrade, out string? rejection)
        {
            rejection = null;
            if (candidates.Count == 0)
            {
                rejection = "No active area of the right purpose accepts this material. Add one under Configuration, or let an area accept it.";
                return null;
            }

            var withRoom = candidates.Where(c => c.HasRoom).ToList();
            if (withRoom.Count == 0) { rejection = "Every position that accepts this material is full."; return null; }
            if (!strictGrade) return withRoom[0];

            var grade = GradeOf(item);
            var compatible = withRoom.Where(c => AcceptsGrade(c, grade)).ToList();
            if (compatible.Count == 0)
            {
                rejection = $"Grade separation is on and every position with room already holds a different grade (incoming '{grade}').";
                return null;
            }
            return compatible[0];
        }

        private static bool AcceptsGrade(YardCandidatePosition c, string grade)
            => c.occupied == 0 || string.IsNullOrWhiteSpace(c.distinct_grades)
               || c.distinct_grades.Split(',', StringSplitOptions.RemoveEmptyEntries)
                   .All(g => string.Equals(g.Trim(), grade, StringComparison.OrdinalIgnoreCase));

        /// <summary>Dispatch: refuses a held item, then retrieves it to the dispatch area.</summary>
        public static Task<IReadOnlyList<long>> PlanDispatchAsync(Context ctx, YardItem item, long dispatchZoneId, long? dispatchPositionId)
        {
            if (string.Equals(item.quality_status, YardQualityStatus.LockedQa, StringComparison.OrdinalIgnoreCase))
                throw new YardRuleException($"QA HOLD: item '{item.item_code}' is under review and cannot be moved.");
            return PlanRetrievalAsync(ctx, item, YardOrderType.Dispatch, dispatchZoneId, dispatchPositionId, null);
        }

        /// <summary>
        /// Rework: deliberately no hold check. An item is sent for rework precisely because it is
        /// under a hold; refusing it for being under one would make the action impossible.
        /// </summary>
        public static async Task<IReadOnlyList<long>> PlanReworkAsync(Context ctx, YardItem item, long reworkActivityId)
        {
            var rework = await CandidatesAsync(ctx, item.material_type_id, YardZonePurpose.Rework);
            var destination = rework.FirstOrDefault(c => c.HasRoom)
                ?? throw new YardRuleException("There is no room in a rework area. Add one under Configuration, or clear space in the existing one.");
            return await PlanRetrievalAsync(ctx, item, YardOrderType.Rework, destination.zone_id, destination.position_id, reworkActivityId);
        }

        /// <summary>
        /// Getting one item out and somewhere else. In a digging style, every item above becomes a
        /// DIGGING order to a reserved shuffling or storage position first; then the final order.
        /// Reservations are held in memory across the plan so two digs never target the same slot.
        /// </summary>
        public static async Task<IReadOnlyList<long>> PlanRetrievalAsync(
            Context ctx, YardItem item, string finalOrderType, long destinationZoneId, long? destinationPositionId, long? reworkActivityId)
        {
            if (!item.position_id.HasValue)
                throw new YardRuleException($"Item '{item.item_code}' is not in a position, so it cannot be retrieved.");

            var orders = new List<long>();

            if (YardStorageStyle.Digs(item.storage_style))
            {
                var above = (await ctx.Connection.QueryAsync<YardItem>(new CommandDefinition(Sql(YardItemQueries.GetItemsAbove, ctx.Schema),
                    new { position_id = item.position_id, stack_index = item.stack_index ?? 0 }, ctx.Transaction))).ToList();

                if (above.Count > 0)
                {
                    var shuffling = await CandidatesAsync(ctx, item.material_type_id, YardZonePurpose.Shuffling);
                    var storage = await CandidatesAsync(ctx, item.material_type_id, YardZonePurpose.Storage);
                    var destinations = shuffling.Concat(storage).Where(c => c.position_id != item.position_id).ToList();

                    foreach (var blocker in above)
                    {
                        var dest = destinations.FirstOrDefault(c => c.HasRoom)
                            ?? throw new YardRuleException("Yard is full: there is nowhere to move the items stacked above this one.");
                        dest.reserved++;
                        orders.Add(await RaiseOrderAsync(ctx, YardOrderType.Digging, blocker.item_id,
                            blocker.zone_id, blocker.position_id, dest.zone_id, dest.position_id, null));
                    }
                }
            }

            orders.Add(await RaiseOrderAsync(ctx, finalOrderType, item.item_id, item.zone_id, item.position_id, destinationZoneId, destinationPositionId, reworkActivityId));
            return orders;
        }

        public static async Task<List<YardCandidatePosition>> CandidatesAsync(Context ctx, long materialTypeId, string? zonePurpose)
            => (await ctx.Connection.QueryAsync<YardCandidatePosition>(new CommandDefinition(Sql(YardItemQueries.GetCandidatePositions, ctx.Schema),
                new { material_type_id = materialTypeId, zone_purpose = zonePurpose }, ctx.Transaction))).ToList();

        private static Task<long> RaiseOrderAsync(Context ctx, string orderType, long itemId,
            long? sourceZoneId, long? sourcePositionId, long? destinationZoneId, long? destinationPositionId, long? reworkActivityId)
            => ctx.Connection.ExecuteScalarAsync<long>(new CommandDefinition(Sql(YardWorkOrderQueries.Insert, ctx.Schema), new
            {
                order_type = orderType, item_id = itemId,
                source_zone_id = sourceZoneId, source_position_id = sourcePositionId,
                destination_zone_id = destinationZoneId, destination_position_id = destinationPositionId,
                rework_activity_id = reworkActivityId,
                user_id = User(ctx.UserId),
            }, ctx.Transaction));

        private static Task HoldAsync(Context ctx, long itemId)
            => ctx.Connection.ExecuteAsync(new CommandDefinition(Sql(YardItemQueries.UpdateItemStatus, ctx.Schema),
                new { item_id = itemId, item_status = YardItemStatus.Hold, user_id = User(ctx.UserId) }, ctx.Transaction));

        private static string GradeOf(YardItem item) => string.IsNullOrWhiteSpace(item.grade_code) ? DefaultGrade : item.grade_code.Trim();

        private static bool IsOn(IReadOnlyDictionary<string, string> config, string key)
            => config.TryGetValue(key, out var v) && string.Equals(v?.Trim(), "true", StringComparison.OrdinalIgnoreCase);

        private static string Sql(string sql, string schema) => sql.Replace("{schema}", schema);
        private static long? User(long userId) => userId == 0 ? null : userId;
    }
}
