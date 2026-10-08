using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Mvc;
using Primworth_API.IRepository;
using Primworth_API.Models.Yard;
using Primworth_API.Repository.Yard;
using Primworth_API.Services.Permissions;

namespace Primworth_API.Controllers.Yard
{
    /// <summary>mSYMS-shaped API for the embedded screens: zones, slabs and config.</summary>
    [Route("api/yard/compat")]
    [ApiController]
    [Authorize]
    public class YardCompatController : YardCompatBase
    {
        private readonly IYardCompatRepository _compat;
        private readonly IYardZoneRepository _zones;
        private readonly IYardItemRepository _items;

        public YardCompatController(IYardCompatRepository compat, IYardZoneRepository zones, IYardItemRepository items,
            IYardSchemaContext schema, ILogger<YardCompatController> logger) : base(schema, logger)
        {
            _compat = compat;
            _zones = zones;
            _items = items;
        }

        // ── Zones ────────────────────────────────────────────────────────────────

        /// <summary>GET compat/zones: zones with nested columns and slabs, the whole yard in one call.</summary>
        [RequiresPermission(Dashboard, Operator, Quality, Mill, Admin)]
        [HttpGet("zones")]
        public Task<IActionResult> GetZones([FromQuery] long? projectId, [FromQuery] long? subProjectId)
            => RunRead(async schema =>
            {
                var o = await _compat.GetOverviewAsync(schema);
                var itemsByPosition = o.items.GroupBy(i => i.position_id).ToDictionary(g => g.Key, g => g.OrderBy(i => i.stack_index).ToList());
                var positionsByZone = o.positions.GroupBy(p => p.zone_id).ToDictionary(g => g.Key, g => g.ToList());

                return Ok(o.zones.Select(z =>
                {
                    var positions = positionsByZone.TryGetValue(z.zone_id, out var list) ? list : new List<YardCompatPositionRow>();
                    return new
                    {
                        id = z.zone_id.ToString(),
                        name = z.zone_name,
                        type = ToScreenType(z.zone_purpose),
                        storage_style = z.storage_style,
                        is_active = z.is_active,
                        // Derived from the positions themselves, so it cannot drift from them.
                        layout_config = positions.Count == 0 ? null : new { rows = positions.Max(p => p.row_index), cols = positions.Max(p => p.col_index) },
                        is_compromised = false,
                        columns = positions.Select(p => new
                        {
                            id = p.position_id.ToString(),
                            zone_id = z.zone_id.ToString(),
                            p.row_index,
                            p.col_index,
                            p.capacity_limit,
                            slabs = (itemsByPosition.TryGetValue(p.position_id, out var stacked) ? stacked : new List<YardCompatItemRow>()).Select(SlabShape),
                        }),
                    };
                }));
            }, projectId, subProjectId, Array.Empty<object>());

        /// <summary>
        /// POST compat/zones: the Configurator's "Create Zone". The screen describes the grid cell by
        /// cell (zero-based); the yard creates a uniform grid (one-based) and corrects the odd cells.
        /// The new area accepts every active material type, as a slab plant has one.
        /// </summary>
        [RequiresPermission(Admin)]
        [HttpPost("zones")]
        public Task<IActionResult> CreateZone([FromQuery] long? projectId, [FromQuery] long? subProjectId, [FromBody] CompatZoneBody body)
            => Run(async schema =>
            {
                var cells = body.columns ?? new List<CompatColumnBody>();
                if (cells.Count == 0) return BadRequest(new { error = "A zone needs at least one pit. Set the grid size and try again." });

                var uniform = cells.GroupBy(c => c.capacity_limit).OrderByDescending(g => g.Count()).First().Key;
                var types = (await _zones.GetMaterialTypesAsync(schema, false)).Select(t => t.type_key).ToList();

                var input = new YardZoneInput
                {
                    zone_name = body.name ?? "",
                    zone_purpose = FromScreenType(body.type),
                    storage_style = string.IsNullOrWhiteSpace(body.storage_style) ? YardStorageStyle.Stack : body.storage_style!,
                    material_type_keys = types,
                    rows = cells.Max(c => c.row_index) + 1,
                    cols = cells.Max(c => c.col_index) + 1,
                    capacity_limit = uniform,
                };
                var problem = input.Validate();
                if (problem != null) return BadRequest(new { error = problem });

                var zoneId = await _zones.CreateZoneAsync(schema, input, Schema.CurrentUserId());

                var odd = cells.Where(c => c.capacity_limit != uniform).ToList();
                if (odd.Count > 0)
                {
                    var created = (await _zones.GetPositionsAsync(schema, zoneId)).ToDictionary(p => (p.row_index, p.col_index), p => p.position_id);
                    foreach (var cell in odd)
                        if (created.TryGetValue((cell.row_index + 1, cell.col_index + 1), out var positionId))
                            await _zones.UpdatePositionCapacityAsync(schema, positionId, cell.capacity_limit, Schema.CurrentUserId());
                }

                return Ok(new { id = zoneId.ToString(), name = body.name, type = body.type });
            }, projectId, subProjectId);

        /// <summary>PUT compat/zones/columns/{id}: the capacity editor on the dashboard.</summary>
        [RequiresPermission(Admin)]
        [HttpPut("zones/columns/{columnId:long}")]
        public Task<IActionResult> UpdateColumn(long columnId, [FromQuery] long? projectId, [FromQuery] long? subProjectId, [FromBody] CompatCapacityBody body)
            => Run(async schema =>
            {
                if (body.capacity_limit is < 0 or > 1000) return BadRequest(new { error = "Capacity must be between 0 and 1000." });
                await _zones.UpdatePositionCapacityAsync(schema, columnId, body.capacity_limit, Schema.CurrentUserId());
                return Ok(new { ok = true });
            }, projectId, subProjectId);

        /// <summary>DELETE compat/zones/{id}: refused while the area still holds items.</summary>
        [RequiresPermission(Admin)]
        [HttpDelete("zones/{zoneId:long}")]
        public Task<IActionResult> DeleteZone(long zoneId, [FromQuery] long? projectId, [FromQuery] long? subProjectId)
            => Run(async schema => await _zones.DeleteZoneAsync(schema, zoneId, Schema.CurrentUserId())
                ? Ok(new { ok = true }) : NotFound(new { error = "That zone no longer exists." }), projectId, subProjectId);

        // ── Slabs ────────────────────────────────────────────────────────────────

        [RequiresPermission(Dashboard, Operator, Quality, Mill, Admin)]
        [HttpGet("slabs")]
        public Task<IActionResult> GetSlabs([FromQuery] long? projectId, [FromQuery] long? subProjectId, [FromQuery] string? quality_status)
            => RunRead(async schema => Ok((await _compat.GetItemsAsync(schema, quality_status)).Select(FullSlabShape)), projectId, subProjectId, Array.Empty<object>());

        [RequiresPermission(Dashboard, Mill, Admin)]
        [HttpGet("slabs/dispatch")]
        public Task<IActionResult> DispatchCandidates([FromQuery] long? projectId, [FromQuery] long? subProjectId)
            => RunRead(async schema => Ok((await _compat.GetDispatchCandidatesAsync(schema)).Select(FullSlabShape)), projectId, subProjectId, Array.Empty<object>());

        /// <summary>POST compat/slabs/ship: the slab leaves the yard at the dispatch station.</summary>
        [RequiresPermission(Mill)]
        [HttpPost("slabs/ship")]
        public Task<IActionResult> Ship([FromQuery] long? projectId, [FromQuery] long? subProjectId, [FromBody] CompatSlabBody body)
            => Run(async schema =>
            {
                await _items.RemoveFromYardAsync(schema, body.slab_id, YardItemStatus.Dispatched, YardOrderType.Dispatch, Schema.CurrentUserId());
                return Ok(new { ok = true });
            }, projectId, subProjectId);

        [RequiresPermission(Admin)]
        [HttpPost("slabs/scrap")]
        public Task<IActionResult> Scrap([FromQuery] long? projectId, [FromQuery] long? subProjectId, [FromBody] CompatSlabBody body)
            => Run(async schema =>
            {
                await _items.RemoveFromYardAsync(schema, body.slab_id, YardItemStatus.Scrapped, "SCRAP", Schema.CurrentUserId());
                return Ok(new { ok = true });
            }, projectId, subProjectId);

        // ── Config: the Rule Book's flat [{key, value}] list ─────────────────────

        [RequiresPermission(Dashboard, Operator, Quality, Mill, Admin)]
        [HttpGet("config")]
        public Task<IActionResult> GetConfig([FromQuery] long? projectId, [FromQuery] long? subProjectId)
            => RunRead(async schema => Ok((await _zones.GetConfigAsync(schema)).OrderBy(kv => kv.Key).Select(kv => new { key = kv.Key, value = kv.Value })),
                projectId, subProjectId, Array.Empty<object>());

        [RequiresPermission(Admin)]
        [HttpPut("config/{key}")]
        public Task<IActionResult> SetConfig(string key, [FromQuery] long? projectId, [FromQuery] long? subProjectId, [FromBody] CompatValueBody body)
            => Run(async schema =>
            {
                if (string.IsNullOrWhiteSpace(key) || key.Length > 64) return BadRequest(new { error = "A configuration key is required." });
                await _zones.SetConfigAsync(schema, key.Trim(), body.value, Schema.CurrentUserId());
                return Ok(new { key, value = body.value });
            }, projectId, subProjectId);

        // ── Request bodies ───────────────────────────────────────────────────────
        public class CompatCapacityBody { public int capacity_limit { get; set; } }
        public class CompatSlabBody { public long slab_id { get; set; } }
        public class CompatValueBody { public string? value { get; set; } }

        public class CompatZoneBody
        {
            public string? name { get; set; }
            public string? type { get; set; }
            public string? storage_style { get; set; }
            public List<CompatColumnBody>? columns { get; set; }
        }

        /// <summary>Row and column are zero-based here, the screen's convention, not the yard's.</summary>
        public class CompatColumnBody
        {
            public int row_index { get; set; }
            public int col_index { get; set; }
            public int capacity_limit { get; set; }
        }
    }
}
