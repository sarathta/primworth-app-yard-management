using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Mvc;
using Primworth_API.IRepository;
using Primworth_API.Models.Yard;
using Primworth_API.Repository.Yard;
using Primworth_API.Services.Permissions;

namespace Primworth_API.Controllers.Yard
{
    /// <summary>The crane's worklist and the yard's history.</summary>
    [ApiController]
    [Route("api/yard/work-orders")]
    [Authorize]
    [RequiresPermission("YRDD", "YRDO", "YRDA")]
    public class YardWorkOrderController : YardControllerBase
    {
        private readonly IYardWorkOrderRepository _orders;

        public YardWorkOrderController(IYardWorkOrderRepository orders, IYardSchemaContext schema, ILogger<YardWorkOrderController> logger)
            : base(schema, logger) => _orders = orders;

        /// <summary>GET api/yard/work-orders?orderType= — everything not yet finished, digs first.</summary>
        [HttpGet]
        public Task<IActionResult> GetActive([FromQuery] string? orderType, [FromQuery] long? projectId, [FromQuery] long? subProjectId)
            => Run(async schema => Ok(Envelope(await _orders.GetActiveOrdersAsync(schema, orderType), "Work orders fetched.")), projectId, subProjectId);

        /// <summary>PUT api/yard/work-orders/{id}/complete — the item moves.</summary>
        [HttpPut("{id:long}/complete")]
        [RequiresPermission("YRDO")]
        public Task<IActionResult> Complete(long id, [FromQuery] long? projectId, [FromQuery] long? subProjectId)
            => Run(async schema =>
            {
                var order = await _orders.CompleteAsync(schema, id, Schema.CurrentUserId());
                return Ok(Envelope(order, "Order completed."));
            }, projectId, subProjectId);

        /// <summary>PUT api/yard/work-orders/{id}/flag — the operator could not carry it out; the reason is required.</summary>
        [HttpPut("{id:long}/flag")]
        [RequiresPermission("YRDO")]
        public Task<IActionResult> Flag(long id, [FromBody] YardFlagInput input, [FromQuery] long? projectId, [FromQuery] long? subProjectId)
            => Run(async schema =>
            {
                if (string.IsNullOrWhiteSpace(input.reason))
                    return BadRequest(Envelope<bool?>(null, "Say what stopped you, so the next shift knows why this order came back."));
                return await _orders.FlagAsync(schema, id, input.reason, Schema.CurrentUserId())
                    ? Ok(Envelope(true, "Order flagged.")) : NotFound(Envelope<bool?>(null, "Work order not found, or already completed."));
            }, projectId, subProjectId);

        /// <summary>
        /// POST api/yard/work-orders/dispatch — plan taking one item out.
        /// In a stack, one DIGGING order per item above it comes first; the message says so.
        /// </summary>
        [HttpPost("dispatch")]
        [RequiresPermission("YRDO", "YRDA")]
        public Task<IActionResult> Dispatch([FromBody] YardDispatchInput input, [FromQuery] long? projectId, [FromQuery] long? subProjectId)
            => Run(async schema =>
            {
                var orders = await _orders.PlanDispatchAsync(schema, input.item_id, input.dispatch_zone_id, Schema.CurrentUserId());
                var digs = orders.Count - 1;
                var message = digs > 0
                    ? $"Dispatch queued. {digs} item(s) on top of it must be moved first; the dig orders are on the worklist."
                    : "Dispatch queued and on the worklist.";
                return Ok(Envelope(orders, message));
            }, projectId, subProjectId);

        /// <summary>GET api/yard/work-orders/activity-feed?limit=50</summary>
        [HttpGet("activity-feed")]
        public Task<IActionResult> ActivityFeed([FromQuery] int limit, [FromQuery] long? projectId, [FromQuery] long? subProjectId)
            => Run(async schema => Ok(Envelope(await _orders.GetActivityFeedAsync(schema, limit), "Activity fetched.")), projectId, subProjectId);

        /// <summary>GET api/yard/work-orders/items/{itemId}/history</summary>
        [HttpGet("items/{itemId:long}/history")]
        public Task<IActionResult> ItemHistory(long itemId, [FromQuery] long? projectId, [FromQuery] long? subProjectId)
            => Run(async schema => Ok(Envelope(await _orders.GetItemHistoryAsync(schema, itemId), "History fetched.")), projectId, subProjectId);
    }
}
