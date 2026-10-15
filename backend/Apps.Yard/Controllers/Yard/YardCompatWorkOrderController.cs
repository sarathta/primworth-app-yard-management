using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Mvc;
using Primworth_API.IRepository;
using Primworth_API.Models.Yard;
using Primworth_API.Repository.Yard;
using Primworth_API.Services.Permissions;

namespace Primworth_API.Controllers.Yard
{
    /// <summary>mSYMS-shaped API for the embedded screens: work orders, quality, rework and the mill.</summary>
    [Route("api/yard/compat/workorders")]
    [ApiController]
    [Authorize]
    public class YardCompatWorkOrderController : YardCompatBase
    {
        private readonly IYardCompatRepository _compat;
        private readonly IYardWorkOrderRepository _orders;
        private readonly IYardItemRepository _items;
        private readonly IYardZoneRepository _zones;
        private readonly IRolePermissionReader _permissions;

        public YardCompatWorkOrderController(IYardCompatRepository compat, IYardWorkOrderRepository orders, IYardItemRepository items,
            IYardZoneRepository zones, IRolePermissionReader permissions, IYardSchemaContext schema, ILogger<YardCompatWorkOrderController> logger)
            : base(schema, logger)
        {
            _compat = compat;
            _orders = orders;
            _items = items;
            _zones = zones;
            _permissions = permissions;
        }

        /// <summary>GET compat/workorders: the operator worklist, Prisma-nested.</summary>
        [RequiresPermission(Dashboard, Operator, Mill, Admin)]
        [HttpGet]
        public Task<IActionResult> GetWorkOrders([FromQuery] long? projectId, [FromQuery] long? subProjectId, [FromQuery] string? materialType, [FromQuery] long? yardTypeId)
            => RunRead(async schema => Ok((await _compat.GetWorkOrdersAsync(schema, materialType, yardTypeId)).Select(r => new
            {
                id = r.work_order_id.ToString(),
                type = r.order_type,
                status = r.order_status,
                created_at = r.created_on,
                completed_at = r.completed_at,
                slab_id = r.item_id.ToString(),
                rework_activity_id = r.rework_activity_id,
                flag_reason = r.flag_reason,
                slab = new
                {
                    id = r.item_id.ToString(), slab_code = r.item_code, heat_number = r.heat_number, quality_status = r.quality_status,
                    status = r.item_status, grade_code = r.grade_code, stack_index = r.stack_index, temperature_state = r.temperature_state,
                },
                source_zone = Zone(r.source_zone_id, r.source_zone_name, r.source_zone_purpose),
                destination_zone = Zone(r.destination_zone_id, r.destination_zone_name, r.destination_zone_purpose),
                source_column = Column(r.source_position_id, r.source_row_index, r.source_col_index),
                destination_column = Column(r.destination_position_id, r.destination_row_index, r.destination_col_index),
            })), projectId, subProjectId, Array.Empty<object>());

        /// <summary>PUT compat/workorders/{id}/complete. The tablet sends scanned ids for audits; there are none yet, so the body is ignored.</summary>
        [RequiresPermission(Operator)]
        [HttpPut("{id:long}/complete")]
        public Task<IActionResult> Complete(long id, [FromQuery] long? projectId, [FromQuery] long? subProjectId)
            => Run(async schema =>
            {
                var order = await _orders.CompleteAsync(schema, id, Schema.CurrentUserId());
                return Ok(new { ok = true, id = order?.work_order_id.ToString() });
            }, projectId, subProjectId);

        [RequiresPermission(Operator)]
        [HttpPut("{id:long}/flag")]
        public Task<IActionResult> Flag(long id, [FromQuery] long? projectId, [FromQuery] long? subProjectId, [FromBody] CompatReasonBody? body)
            => Run(async schema =>
            {
                if (string.IsNullOrWhiteSpace(body?.reason))
                    return BadRequest(new { error = "Say what stopped you, so the next shift knows why this order came back." });
                return await _orders.FlagAsync(schema, id, body!.reason!, Schema.CurrentUserId())
                    ? Ok(new { ok = true }) : NotFound(new { error = "Work order not found, or it is already completed." });
            }, projectId, subProjectId);

        [RequiresPermission(Dashboard, Operator, Quality, Mill, Admin)]
        [HttpGet("activity-feed")]
        public Task<IActionResult> ActivityFeed([FromQuery] long? projectId, [FromQuery] long? subProjectId, [FromQuery] string? materialType, [FromQuery] long? yardTypeId)
            => RunRead(async schema => Ok((await _orders.GetActivityFeedAsync(schema, 50, materialType, yardTypeId)).Select(MovementShape)), projectId, subProjectId, Array.Empty<object>());

        [RequiresPermission(Dashboard, Operator, Quality, Mill, Admin)]
        [HttpGet("slab/{slabId:long}/history")]
        public Task<IActionResult> SlabHistory(long slabId, [FromQuery] long? projectId, [FromQuery] long? subProjectId)
            => RunRead(async schema => Ok((await _orders.GetItemHistoryAsync(schema, slabId)).Select(MovementShape)), projectId, subProjectId, Array.Empty<object>());

        /// <summary>
        /// PUT compat/workorders/{slabId}/qa-lock: apply or release a hold. Applying is something an
        /// operator may do; releasing needs the release code, checked here because the direction is in the body.
        /// </summary>
        [RequiresPermission(Quality, Operator)]
        [HttpPut("{slabId:long}/qa-lock")]
        public Task<IActionResult> QaLock(long slabId, [FromQuery] long? projectId, [FromQuery] long? subProjectId, [FromBody] CompatQaBody body)
            => Run(async schema =>
            {
                if (!body.locked && !await _permissions.HasAsync(User, ReleaseQa))
                    return StatusCode(StatusCodes.Status403Forbidden, new { error = "Releasing a hold needs the Release QA Lock permission." });
                return await SetQuality(schema, slabId, body.locked ? YardQualityStatus.LockedQa : YardQualityStatus.Approved);
            }, projectId, subProjectId);

        [RequiresPermission(ReleaseQa)]
        [HttpPut("{slabId:long}/qa-unlock")]
        public Task<IActionResult> QaUnlock(long slabId, [FromQuery] long? projectId, [FromQuery] long? subProjectId)
            => Run(schema => SetQuality(schema, slabId, YardQualityStatus.Approved), projectId, subProjectId);

        private async Task<IActionResult> SetQuality(string schema, long itemId, string status)
        {
            // Reported rather than swallowed: a stale screen must not be told the hold was applied when no row was touched.
            if (!await _items.SetQualityAsync(schema, itemId, status, Schema.CurrentUserId()))
                return NotFound(new { error = "That slab is no longer in the yard. Refresh the list." });
            return Ok(new { ok = true, quality_status = status });
        }

        /// <summary>POST compat/workorders/{slabId}/rework: Quality's verdict on a held slab.</summary>
        [RequiresPermission(Quality)]
        [HttpPost("{slabId:long}/rework")]
        public Task<IActionResult> AssignRework(long slabId, [FromQuery] long? projectId, [FromQuery] long? subProjectId, [FromBody] CompatReworkBody body)
            => Run(async schema =>
            {
                var orders = await _orders.AssignReworkAsync(schema, slabId, body.rework_activity_id, Schema.CurrentUserId());
                return Ok(new { ok = true, work_orders = orders.Select(o => o.ToString()) });
            }, projectId, subProjectId);

        /// <summary>GET compat/workorders/hsm-schedule: the rolling campaign, hottest first.</summary>
        [RequiresPermission(Mill, Admin)]
        [HttpGet("hsm-schedule")]
        public Task<IActionResult> MillSchedule([FromQuery] long? projectId, [FromQuery] long? subProjectId, [FromQuery] int limit = 100, [FromQuery] string? materialType = null, [FromQuery] long? yardTypeId = null)
            => RunRead(async schema => Ok(new { campaign = (await _compat.GetMillCampaignAsync(schema, limit, materialType, yardTypeId)).Select(FullSlabShape) }),
                projectId, subProjectId, new { campaign = Array.Empty<object>() });

        /// <summary>
        /// POST compat/workorders/hsm-transfer: charging a slab into the mill is, from the yard's side,
        /// a dispatch: dug out if buried, then it leaves. Same planner, same hold rule, same reservations.
        /// </summary>
        [RequiresPermission(Mill)]
        [HttpPost("hsm-transfer")]
        public Task<IActionResult> MillTransfer([FromQuery] long? projectId, [FromQuery] long? subProjectId, [FromBody] CompatSlabBody body)
            => Run(async schema =>
            {
                var orders = await _orders.PlanDispatchAsync(schema, body.slab_id, null, Schema.CurrentUserId());
                var digs = orders.Count - 1;
                var message = digs > 0
                    ? $"Transfer queued. {digs} slab(s) stacked above it must be moved first; the dig orders are on the operator worklist."
                    : "Transfer queued and on the operator worklist.";
                return Ok(new { ok = true, message, work_orders = orders.Select(o => o.ToString()) });
            }, projectId, subProjectId);

        public class CompatReasonBody { public string? reason { get; set; } }
        public class CompatQaBody { public bool locked { get; set; } }
        public class CompatReworkBody { public long rework_activity_id { get; set; } }
        public class CompatSlabBody { public long slab_id { get; set; } }
    }
}
