using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Mvc;
using Primworth_API.IRepository;
using Primworth_API.Models.Yard;
using Primworth_API.Repository.Yard;
using Primworth_API.Services.Permissions;

namespace Primworth_API.Controllers.Yard
{
    /// <summary>Yard layout: areas, positions, material types and configuration of the current facility.</summary>
    [ApiController]
    [Route("api/yard/zones")]
    [Authorize]
    [RequiresPermission("YRDD", "YRDO", "YRDA")]
    public class YardZoneController : YardControllerBase
    {
        private readonly IYardZoneRepository _zones;

        public YardZoneController(IYardZoneRepository zones, IYardSchemaContext schema, ILogger<YardZoneController> logger)
            : base(schema, logger) => _zones = zones;

        /// <summary>GET api/yard/zones — every area with its counts and accepted material types.</summary>
        [HttpGet]
        public Task<IActionResult> GetZones([FromQuery] long? projectId, [FromQuery] long? subProjectId)
            => Run(async schema => Ok(Envelope(await _zones.GetZonesAsync(schema), "Areas fetched.")), projectId, subProjectId);

        /// <summary>GET api/yard/zones/{zoneId}/positions — the grid of one area.</summary>
        [HttpGet("{zoneId:long}/positions")]
        public Task<IActionResult> GetPositions(long zoneId, [FromQuery] long? projectId, [FromQuery] long? subProjectId)
            => Run(async schema => Ok(Envelope(await _zones.GetPositionsAsync(schema, zoneId), "Positions fetched.")), projectId, subProjectId);

        /// <summary>GET api/yard/zones/material-types — the words and fields of each material this facility stores.</summary>
        [HttpGet("material-types")]
        public Task<IActionResult> GetMaterialTypes([FromQuery] long? projectId, [FromQuery] long? subProjectId, [FromQuery] bool includeInactive = false)
            => Run(async schema => Ok(Envelope(await _zones.GetMaterialTypesAsync(schema, includeInactive), "Material types fetched.")), projectId, subProjectId);

        /// <summary>GET api/yard/zones/config — feature flags and tuning values.</summary>
        [HttpGet("config")]
        public Task<IActionResult> GetConfig([FromQuery] long? projectId, [FromQuery] long? subProjectId)
            => Run(async schema => Ok(Envelope(await _zones.GetConfigAsync(schema), "Configuration fetched.")), projectId, subProjectId);

        /// <summary>POST api/yard/zones — an area with its accepted materials and position grid.</summary>
        [HttpPost]
        [RequiresPermission("YRDA")]
        public Task<IActionResult> CreateZone([FromBody] YardZoneInput input, [FromQuery] long? projectId, [FromQuery] long? subProjectId)
            => Run(async schema =>
            {
                var problem = input.Validate();
                if (problem != null) return BadRequest(Envelope<long?>(null, problem));
                var id = await _zones.CreateZoneAsync(schema, input, Schema.CurrentUserId());
                return Ok(Envelope(id, $"Area {input.zone_name.Trim()} added successfully."));
            }, projectId, subProjectId);

        /// <summary>PUT api/yard/zones/positions/{positionId}/capacity</summary>
        [HttpPut("positions/{positionId:long}/capacity")]
        [RequiresPermission("YRDA")]
        public Task<IActionResult> SetCapacity(long positionId, [FromBody] YardCapacityInput input, [FromQuery] long? projectId, [FromQuery] long? subProjectId)
            => Run(async schema =>
            {
                if (input.capacity_limit is < 0 or > 1000) return BadRequest(Envelope<bool?>(null, "capacity_limit must be between 0 and 1000."));
                return await _zones.UpdatePositionCapacityAsync(schema, positionId, input.capacity_limit, Schema.CurrentUserId())
                    ? Ok(Envelope(true, "Capacity updated."))
                    : NotFound(Envelope<bool?>(null, "Position not found."));
            }, projectId, subProjectId);

        /// <summary>PUT api/yard/zones/config/{key}</summary>
        [HttpPut("config/{key}")]
        [RequiresPermission("YRDA")]
        public Task<IActionResult> SetConfig(string key, [FromBody] YardConfigInput input, [FromQuery] long? projectId, [FromQuery] long? subProjectId)
            => Run(async schema =>
            {
                if (string.IsNullOrWhiteSpace(key) || key.Length > 64) return BadRequest(Envelope<bool?>(null, "A configuration key is required (max 64 characters)."));
                await _zones.SetConfigAsync(schema, key.Trim(), input.value, Schema.CurrentUserId());
                return Ok(Envelope(true, $"{key.Trim()} saved."));
            }, projectId, subProjectId);

        /// <summary>DELETE api/yard/zones/{zoneId} — refused while the area still holds items.</summary>
        [HttpDelete("{zoneId:long}")]
        [RequiresPermission("YRDA")]
        public Task<IActionResult> DeleteZone(long zoneId, [FromQuery] long? projectId, [FromQuery] long? subProjectId)
            => Run(async schema => await _zones.DeleteZoneAsync(schema, zoneId, Schema.CurrentUserId())
                ? Ok(Envelope(true, "Area deleted."))
                : NotFound(Envelope<bool?>(null, "Area not found.")), projectId, subProjectId);

        public class YardConfigInput { public string? value { get; set; } }
    }
}
