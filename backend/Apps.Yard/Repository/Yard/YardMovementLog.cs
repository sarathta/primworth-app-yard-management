using System.Data;
using Dapper;
using Primworth_API.Models.Yard;
using Primworth_API.Queries.Yard;

namespace Primworth_API.Repository.Yard
{
    /// <summary>
    /// The two logs every yard write appends to, written once. Names are copied in rather than
    /// joined so history stays readable after an area is renamed or a user is deactivated.
    /// </summary>
    public static class YardMovementLog
    {
        public static async Task WriteMovementAsync(IDbConnection connection, IDbTransaction tx, string schema, long userId,
            long itemId, long? fromZoneId, long? toZoneId, string? fromZone, string? toZone,
            string? fromPosition, string? toPosition, string? orderType)
        {
            var name = userId > 0
                ? await connection.ExecuteScalarAsync<string>(new CommandDefinition(YardWorkOrderQueries.GetUserDisplayName, new { user_id = userId }, tx))
                : null;

            await connection.ExecuteAsync(new CommandDefinition(YardWorkOrderQueries.InsertMovement.Replace("{schema}", schema), new
            {
                item_id = itemId, from_zone_id = fromZoneId, to_zone_id = toZoneId,
                from_zone_name = fromZone, to_zone_name = toZone, from_position = fromPosition, to_position = toPosition,
                order_type = orderType ?? YardOrderType.Move, performed_by = userId > 0 ? userId : (long?)null, performed_name = name,
            }, tx));
        }

        /// <summary>One line in the activity log. `details` is serialised to JSON.</summary>
        public static Task WriteSystemLogAsync(IDbConnection connection, IDbTransaction? tx, string schema, long userId, string action, object details)
            => connection.ExecuteAsync(new CommandDefinition(YardMasterQueries.InsertSystemLog.Replace("{schema}", schema), new
            {
                user_id = userId > 0 ? userId : (long?)null,
                action,
                details = System.Text.Json.JsonSerializer.Serialize(details),
            }, tx));
    }
}
