using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Mvc;
using Primworth_API.IRepository;
using Primworth_API.Models.Yard;
using Primworth_API.Repository.Yard;
using Primworth_API.Services.Permissions;

namespace Primworth_API.Controllers.Yard
{
    /// <summary>
    /// mSYMS-shaped API for the Configurator's master data and activity log.
    ///
    /// The screen calls them grades, dimensions, rework and productmix; the yard calls them
    /// gm_grade, size profiles, rework activities and product mixes. Field names are the screen's.
    /// Every number arrives as a string because the forms use text boxes.
    /// </summary>
    [Route("api/yard/compat")]
    [ApiController]
    [Authorize]
    public class YardCompatMastersController : YardCompatBase
    {
        private readonly IYardMasterRepository _masters;

        public YardCompatMastersController(IYardMasterRepository masters, IYardSchemaContext schema, ILogger<YardCompatMastersController> logger)
            : base(schema, logger) => _masters = masters;

        // ── Grades: the platform's master, read-only here ────────────────────────
        [RequiresPermission(Dashboard, Operator, Quality, Mill, Admin)]
        [HttpGet("masters/grades")]
        public Task<IActionResult> Grades([FromQuery] long? projectId, [FromQuery] long? subProjectId)
            => RunRead(async schema => Ok((await _masters.GetGradesAsync(schema)).Select(g => new { g.id, code = g.grade_code, name = g.grade_code, g.description })),
                projectId, subProjectId, Array.Empty<object>());

        // ── Dimensions = size profiles ───────────────────────────────────────────
        [RequiresPermission(Dashboard, Operator, Quality, Mill, Admin)]
        [HttpGet("masters/dimensions")]
        public Task<IActionResult> Dimensions([FromQuery] long? projectId, [FromQuery] long? subProjectId)
            => RunRead(async schema => Ok((await _masters.GetSizeProfilesAsync(schema)).Select(d => new
            {
                id = d.size_profile_id, name = d.profile_name, d.length_mm, d.width_mm, d.thickness_mm, d.tolerance_pct, d.is_active,
            })), projectId, subProjectId, Array.Empty<object>());

        [RequiresPermission(Admin)]
        [HttpPost("masters/dimensions")]
        public Task<IActionResult> CreateDimension([FromQuery] long? projectId, [FromQuery] long? subProjectId, [FromBody] CompatDimensionBody body)
            => SaveDimension(projectId, subProjectId, null, body);

        [RequiresPermission(Admin)]
        [HttpPut("masters/dimensions/{id:long}")]
        public Task<IActionResult> UpdateDimension(long id, [FromQuery] long? projectId, [FromQuery] long? subProjectId, [FromBody] CompatDimensionBody body)
            => SaveDimension(projectId, subProjectId, id, body);

        private Task<IActionResult> SaveDimension(long? projectId, long? subProjectId, long? id, CompatDimensionBody body)
            => Run(async schema =>
            {
                // A blank or mistyped measurement is refused rather than silently becoming zero.
                if (!Decimal(body.length_mm, out var length)) return BadRequest(new { error = "Length must be a number." });
                if (!Decimal(body.width_mm, out var width)) return BadRequest(new { error = "Width must be a number." });
                if (!Decimal(body.thickness_mm, out var thickness)) return BadRequest(new { error = "Thickness must be a number." });
                if (!Decimal(body.tolerance_pct, out var tolerance)) return BadRequest(new { error = "Tolerance must be a number." });

                var input = new YardSizeProfileInput { profile_name = body.name ?? "", length_mm = length, width_mm = width, thickness_mm = thickness, tolerance_pct = tolerance };
                var problem = input.Validate();
                if (problem != null) return BadRequest(new { error = problem });

                var saved = await _masters.SaveSizeProfileAsync(schema, id, input, Schema.CurrentUserId());
                return Ok(new { id = saved.ToString() });
            }, projectId, subProjectId);

        [RequiresPermission(Admin)]
        [HttpDelete("masters/dimensions/{id:long}")]
        public Task<IActionResult> DeleteDimension(long id, [FromQuery] long? projectId, [FromQuery] long? subProjectId)
            => Run(async schema => await _masters.DeleteSizeProfileAsync(schema, id, Schema.CurrentUserId())
                ? Ok(new { ok = true }) : NotFound(new { error = "That dimension profile no longer exists." }), projectId, subProjectId);

        // ── Rework activities ────────────────────────────────────────────────────
        [RequiresPermission(Dashboard, Operator, Quality, Mill, Admin)]
        [HttpGet("masters/rework")]
        public Task<IActionResult> Rework([FromQuery] long? projectId, [FromQuery] long? subProjectId)
            => RunRead(async schema => Ok((await _masters.GetReworkActivitiesAsync(schema)).Select(a => new
            {
                id = a.rework_activity_id, name = a.activity_name, a.description, a.is_active,
            })), projectId, subProjectId, Array.Empty<object>());

        [RequiresPermission(Admin)]
        [HttpPost("masters/rework")]
        public Task<IActionResult> CreateRework([FromQuery] long? projectId, [FromQuery] long? subProjectId, [FromBody] CompatNamedBody body)
            => SaveRework(projectId, subProjectId, null, body);

        [RequiresPermission(Admin)]
        [HttpPut("masters/rework/{id:long}")]
        public Task<IActionResult> UpdateRework(long id, [FromQuery] long? projectId, [FromQuery] long? subProjectId, [FromBody] CompatNamedBody body)
            => SaveRework(projectId, subProjectId, id, body);

        private Task<IActionResult> SaveRework(long? projectId, long? subProjectId, long? id, CompatNamedBody body)
            => Run(async schema =>
            {
                var input = new YardReworkActivityInput { activity_name = body.name ?? "", description = body.description };
                var problem = input.Validate();
                if (problem != null) return BadRequest(new { error = problem });
                var saved = await _masters.SaveReworkActivityAsync(schema, id, input, Schema.CurrentUserId());
                return Ok(new { id = saved.ToString() });
            }, projectId, subProjectId);

        [RequiresPermission(Admin)]
        [HttpDelete("masters/rework/{id:long}")]
        public Task<IActionResult> DeleteRework(long id, [FromQuery] long? projectId, [FromQuery] long? subProjectId)
            => Run(async schema => await _masters.DeleteReworkActivityAsync(schema, id, Schema.CurrentUserId())
                ? Ok(new { ok = true }) : NotFound(new { error = "That rework activity no longer exists." }), projectId, subProjectId);

        // ── Product mix ──────────────────────────────────────────────────────────
        [RequiresPermission(Dashboard, Operator, Quality, Mill, Admin)]
        [HttpGet("masters/productmix")]
        public Task<IActionResult> ProductMixes([FromQuery] long? projectId, [FromQuery] long? subProjectId)
            => RunRead(async schema => Ok((await _masters.GetProductMixesAsync(schema)).Select(m => new
            {
                id = m.product_mix_id.ToString(),
                name = m.mix_name,
                m.description,
                target_zone_type = ToScreenType(m.target_zone_purpose),
                m.priority,
                m.is_active,
                allowed_grades = m.AllowedGrades,
                // Ids as strings: the screen compares them against the dimension list's string ids.
                allowed_dimensions = m.AllowedSizeIds.Select(i => i.ToString()),
            })), projectId, subProjectId, Array.Empty<object>());

        [RequiresPermission(Admin)]
        [HttpPost("masters/productmix")]
        public Task<IActionResult> CreateProductMix([FromQuery] long? projectId, [FromQuery] long? subProjectId, [FromBody] CompatMixBody body)
            => SaveProductMix(projectId, subProjectId, 0, body);

        [RequiresPermission(Admin)]
        [HttpPut("masters/productmix/{id:long}")]
        public Task<IActionResult> UpdateProductMix(long id, [FromQuery] long? projectId, [FromQuery] long? subProjectId, [FromBody] CompatMixBody body)
            => SaveProductMix(projectId, subProjectId, id, body);

        private Task<IActionResult> SaveProductMix(long? projectId, long? subProjectId, long id, CompatMixBody body)
            => Run(async schema =>
            {
                var input = new YardProductMixInput
                {
                    mix_name = body.name ?? "",
                    description = body.description,
                    target_zone_purpose = FromScreenType(body.target_zone_type),
                    priority = int.TryParse(body.priority, out var p) ? p : 0,
                    allowed_grades = body.allowed_grades ?? new List<string>(),
                    allowed_size_profile_ids = (body.allowed_dimensions ?? new List<string>()).Select(v => long.TryParse(v, out var n) ? n : 0).Where(n => n > 0).ToList(),
                };
                var problem = input.Validate();
                if (problem != null) return BadRequest(new { error = problem });
                var saved = await _masters.SaveProductMixAsync(schema, id, input, Schema.CurrentUserId());
                return Ok(new { id = saved.ToString() });
            }, projectId, subProjectId);

        [RequiresPermission(Admin)]
        [HttpDelete("masters/productmix/{id:long}")]
        public Task<IActionResult> DeleteProductMix(long id, [FromQuery] long? projectId, [FromQuery] long? subProjectId)
            => Run(async schema => await _masters.DeleteProductMixAsync(schema, id, Schema.CurrentUserId())
                ? Ok(new { ok = true }) : NotFound(new { error = "That product mix no longer exists." }), projectId, subProjectId);

        // ── Activity log: the screen reads { logs, total } ───────────────────────
        [RequiresPermission(Admin)]
        [HttpGet("logs")]
        public Task<IActionResult> Logs([FromQuery] long? projectId, [FromQuery] long? subProjectId,
            [FromQuery] int page = 1, [FromQuery] int limit = 50, [FromQuery] string? action = null, [FromQuery] DateTime? from = null, [FromQuery] DateTime? to = null)
            => RunRead(async schema =>
            {
                var (rows, total) = await _masters.GetSystemLogsAsync(schema, page, limit, action, from, to);
                return Ok(new
                {
                    logs = rows.Select(r => new { id = r.id.ToString(), user_id = r.user_id?.ToString(), r.action, details = Json(r.details), r.timestamp }),
                    total,
                });
            }, projectId, subProjectId, new { logs = Array.Empty<object>(), total = 0 });

        // ── Request bodies ───────────────────────────────────────────────────────
        public class CompatNamedBody { public string? name { get; set; } public string? description { get; set; } }

        public class CompatDimensionBody
        {
            public string? name { get; set; }
            public string? length_mm { get; set; }
            public string? width_mm { get; set; }
            public string? thickness_mm { get; set; }
            public string? tolerance_pct { get; set; }
        }

        public class CompatMixBody
        {
            public string? name { get; set; }
            public string? description { get; set; }
            public string? target_zone_type { get; set; }
            public string? priority { get; set; }
            public List<string>? allowed_grades { get; set; }
            public List<string>? allowed_dimensions { get; set; }
        }
    }
}
