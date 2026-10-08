using Primworth_API.Models.Yard;

namespace Primworth_API.IRepository
{
    /// <summary>Work orders and history. Every method takes the already-resolved facility schema.</summary>
    public interface IYardWorkOrderRepository
    {
        Task<IEnumerable<YardWorkOrder>> GetActiveOrdersAsync(string schema, string? orderType);

        /// <summary>Carries an order out: the item physically moves, the log gets a line.</summary>
        Task<YardWorkOrder?> CompleteAsync(string schema, long workOrderId, long userId);

        Task<bool> FlagAsync(string schema, long workOrderId, string reason, long userId);

        /// <summary>
        /// Plans a dispatch: one DIGGING order per item above the wanted one (only in areas whose
        /// storage style digs), each with its own reserved destination, then the DISPATCH order.
        /// </summary>
        Task<IReadOnlyList<long>> PlanDispatchAsync(string schema, long itemId, long? dispatchZoneId, long userId);

        /// <summary>
        /// Quality's verdict on a held item: route it to a rework area with the activity to carry
        /// out. The hold comes off (quality goes back to PENDING, not APPROVED). Digs as dispatch does.
        /// </summary>
        Task<IReadOnlyList<long>> AssignReworkAsync(string schema, long itemId, long reworkActivityId, long userId);

        Task<IEnumerable<YardMovement>> GetActivityFeedAsync(string schema, int limit);
        Task<IEnumerable<YardMovement>> GetItemHistoryAsync(string schema, long itemId);
    }
}
