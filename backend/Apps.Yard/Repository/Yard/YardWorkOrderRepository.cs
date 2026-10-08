using Dapper;
using Primworth_API.IRepository;
using Primworth_API.Models.Yard;
using Primworth_API.Queries.Yard;

namespace Primworth_API.Repository.Yard
{
    /// <summary>
    /// The work order lifecycle, and with it the yard's one physical invariant.
    ///
    /// Completing an order is where an item physically moves, so it is the one place that must
    /// get the stack right: lock the destination, next index = MAX + 1, repack the position it
    /// left. The partial unique index on (position, stack index) makes a violation impossible
    /// rather than unlikely.
    /// </summary>
    public class YardWorkOrderRepository : IYardWorkOrderRepository
    {
        private readonly DapperContext _context;

        public YardWorkOrderRepository(DapperContext context) => _context = context;

        public async Task<IEnumerable<YardWorkOrder>> GetActiveOrdersAsync(string schema, string? orderType)
        {
            using var connection = _context.CreateConnection();
            return await connection.QueryAsync<YardWorkOrder>(Sql(YardWorkOrderQueries.GetActiveOrders, schema),
                new { order_type = string.IsNullOrWhiteSpace(orderType) ? null : orderType.Trim().ToUpperInvariant() });
        }

        public async Task<YardWorkOrder?> CompleteAsync(string schema, long workOrderId, long userId)
        {
            using var connection = _context.CreateConnection();
            connection.Open();
            using var tx = connection.BeginTransaction();

            var order = await connection.QueryFirstOrDefaultAsync<YardWorkOrder>(new CommandDefinition(
                Sql(YardWorkOrderQueries.GetById, schema), new { work_order_id = workOrderId }, tx))
                ?? throw new YardRuleException("Work order not found.");
            if (order.order_status == YardOrderStatus.Completed) throw new YardRuleException("This work order is already completed.");

            var item = await connection.QueryFirstOrDefaultAsync<YardItem>(new CommandDefinition(
                Sql(YardItemQueries.GetItemById, schema), new { item_id = order.item_id }, tx))
                ?? throw new YardRuleException("The item this order refers to no longer exists.");

            // A hold outranks an order raised before the hold was applied.
            if (item.quality_status == YardQualityStatus.LockedQa)
                throw new YardRuleException($"QA HOLD: item '{item.item_code}' is under review and cannot be moved.");

            var fromZone = item.zone_name;
            var fromPosition = item.PositionLabel;
            var previousPosition = item.position_id;
            var previousIndex = item.stack_index;
            string? toZone = null, toPosition = null;

            if (order.destination_position_id.HasValue)
            {
                var dest = await connection.QueryFirstOrDefaultAsync<YardCandidatePosition>(new CommandDefinition(
                    Sql(YardItemQueries.LockPosition, schema), new { position_id = order.destination_position_id.Value }, tx))
                    ?? throw new YardRuleException("The destination position no longer exists.");

                // Re-checked at completion: the yard may have filled up since the order was planned.
                var occupied = await connection.ExecuteScalarAsync<int>(new CommandDefinition(
                    Sql(YardZoneQueries.CountItemsInPosition, schema), new { position_id = dest.position_id }, tx));
                if (occupied >= dest.capacity_limit)
                    throw new YardRuleException($"Destination {dest.zone_name} {dest.PositionLabel} is full ({occupied}/{dest.capacity_limit}). Flag this order.");

                var index = await connection.ExecuteScalarAsync<int>(new CommandDefinition(
                    Sql(YardItemQueries.NextStackIndex, schema), new { position_id = dest.position_id }, tx));

                await connection.ExecuteAsync(new CommandDefinition(Sql(YardItemQueries.PlaceItem, schema), new
                {
                    item_id = item.item_id, zone_id = dest.zone_id, position_id = dest.position_id, stack_index = index,
                    item_status = StatusAfter(order.order_type), user_id = User(userId),
                }, tx));

                toZone = dest.zone_name;
                toPosition = dest.PositionLabel;
            }
            else
            {
                // No destination position: the item leaves the yard.
                await connection.ExecuteAsync(new CommandDefinition(Sql(YardItemQueries.ClearItemLocation, schema),
                    new { item_id = item.item_id, item_status = StatusAfter(order.order_type), user_id = User(userId) }, tx));
            }

            // Leaving a position means closing the gap behind you, or the next MAX+1 drifts.
            if (previousPosition.HasValue && previousIndex.HasValue && previousPosition != order.destination_position_id)
            {
                await connection.ExecuteAsync(new CommandDefinition(Sql(YardItemQueries.RepackAbove, schema),
                    new { position_id = previousPosition.Value, removed_stack_index = previousIndex.Value, user_id = User(userId) }, tx));
            }

            await connection.ExecuteAsync(new CommandDefinition(Sql(YardWorkOrderQueries.Complete, schema),
                new { work_order_id = order.work_order_id, user_id = User(userId) }, tx));

            await YardMovementLog.WriteMovementAsync(connection, tx, schema, userId, item.item_id, item.zone_id, order.destination_zone_id,
                fromZone, toZone, fromPosition, toPosition, order.order_type);

            tx.Commit();

            return await connection.QueryFirstOrDefaultAsync<YardWorkOrder>(Sql(YardWorkOrderQueries.GetById, schema), new { work_order_id = order.work_order_id });
        }

        private static string StatusAfter(string? orderType) => orderType switch
        {
            YardOrderType.Dispatch => YardItemStatus.Dispatched,
            YardOrderType.Rework => YardItemStatus.Rework,
            _ => YardItemStatus.Ready,
        };

        public async Task<bool> FlagAsync(string schema, long workOrderId, string reason, long userId)
        {
            using var connection = _context.CreateConnection();
            connection.Open();
            using var tx = connection.BeginTransaction();

            var affected = await connection.ExecuteAsync(new CommandDefinition(Sql(YardWorkOrderQueries.Flag, schema),
                new { work_order_id = workOrderId, reason = reason.Trim(), user_id = User(userId) }, tx));
            if (affected == 0) { tx.Rollback(); return false; }

            // The log exists so the next shift knows which order stalled and why.
            await YardMovementLog.WriteSystemLogAsync(connection, tx, schema, userId, "WORK_ORDER_FLAGGED", new { work_order_id = workOrderId, reason = reason.Trim() });
            tx.Commit();
            return true;
        }

        public async Task<IReadOnlyList<long>> PlanDispatchAsync(string schema, long itemId, long? dispatchZoneId, long userId)
        {
            using var connection = _context.CreateConnection();
            connection.Open();
            using var tx = connection.BeginTransaction();

            var item = await connection.QueryFirstOrDefaultAsync<YardItem>(new CommandDefinition(
                Sql(YardItemQueries.GetItemById, schema), new { item_id = itemId }, tx))
                ?? throw new YardRuleException("Item not found.");

            var zoneId = dispatchZoneId ?? await connection.ExecuteScalarAsync<long?>(new CommandDefinition(
                Sql(YardZoneQueries.GetFirstActiveZoneOfPurpose, schema), new { zone_purpose = YardZonePurpose.Dispatch }, tx))
                ?? throw new YardRuleException("There is no dispatch area in this yard. Add one under Configuration.");

            var orders = await YardPlacement.PlanDispatchAsync(
                new YardPlacement.Context { Connection = connection, Transaction = tx, Schema = schema, UserId = userId }, item, zoneId, null);

            tx.Commit();
            return orders;
        }

        public async Task<IReadOnlyList<long>> AssignReworkAsync(string schema, long itemId, long reworkActivityId, long userId)
        {
            using var connection = _context.CreateConnection();
            connection.Open();
            using var tx = connection.BeginTransaction();

            var item = await connection.QueryFirstOrDefaultAsync<YardItem>(new CommandDefinition(
                Sql(YardItemQueries.GetItemById, schema), new { item_id = itemId }, tx))
                ?? throw new YardRuleException("Item not found.");

            // Named rather than checked by id alone, so the refusal says which activity is the problem.
            var activity = await connection.QueryFirstOrDefaultAsync<YardReworkActivity>(new CommandDefinition(
                Sql(YardMasterQueries.GetReworkActivityById, schema), new { id = reworkActivityId }, tx))
                ?? throw new YardRuleException("That rework activity no longer exists. Refresh and pick another.");
            if (!activity.is_active) throw new YardRuleException($"Rework activity '{activity.activity_name}' has been retired and cannot be assigned.");

            var orders = await YardPlacement.PlanReworkAsync(
                new YardPlacement.Context { Connection = connection, Transaction = tx, Schema = schema, UserId = userId }, item, activity.rework_activity_id);

            // The hold is resolved by this decision, so it comes off. PENDING rather than APPROVED: the
            // steel is not good yet, it is on its way to being made good.
            await connection.ExecuteAsync(new CommandDefinition(Sql(YardItemQueries.SetQualityStatus, schema),
                new { item_id = item.item_id, quality_status = YardQualityStatus.Pending, user_id = User(userId) }, tx));

            await YardMovementLog.WriteSystemLogAsync(connection, tx, schema, userId, "QA_REWORK_ASSIGNED", new
            {
                item_id = item.item_id, item_code = item.item_code, rework_activity_id = activity.rework_activity_id,
                rework_activity = activity.activity_name, work_orders = orders,
            });

            tx.Commit();
            return orders;
        }

        public async Task<IEnumerable<YardMovement>> GetActivityFeedAsync(string schema, int limit)
        {
            using var connection = _context.CreateConnection();
            return await connection.QueryAsync<YardMovement>(Sql(YardWorkOrderQueries.GetActivityFeed, schema), new { limit = limit is < 1 or > 500 ? 50 : limit });
        }

        public async Task<IEnumerable<YardMovement>> GetItemHistoryAsync(string schema, long itemId)
        {
            using var connection = _context.CreateConnection();
            return await connection.QueryAsync<YardMovement>(Sql(YardWorkOrderQueries.GetItemHistory, schema), new { item_id = itemId });
        }

        private static string Sql(string sql, string schema) => sql.Replace("{schema}", schema);
        private static long? User(long userId) => userId == 0 ? null : userId;
    }
}
