using System.Text.Json;
using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Mvc;
using Primworth_API.IRepository;
using Primworth_API.Repository.Yard;
using Primworth_API.Services.Permissions;

namespace Primworth_API.Controllers.Yard
{
    [ApiController]
    [Route("api/yard/rules")]
    [Authorize]
    [RequiresPermission("YRDA")]
    public class YardRulesController : YardControllerBase
    {
        private static readonly JsonElement EmptyGraph = JsonDocument.Parse("""{"nodes":[],"edges":[]}""").RootElement.Clone();

        private readonly IYardRulesRepository _rules;

        public YardRulesController(IYardRulesRepository rules, IYardSchemaContext schema, ILogger<YardRulesController> logger)
            : base(schema, logger) => _rules = rules;

        [HttpGet]
        public Task<IActionResult> Get([FromQuery] long? projectId, [FromQuery] long? subProjectId, [FromQuery] long? yardTypeId)
            => Run(async schema =>
            {
                if (yardTypeId is not > 0)
                    return BadRequest(Envelope<JsonElement?>(null, "Open a yard, then edit the rules for that yard."));
                var raw = await _rules.GetGraphAsync(schema, yardTypeId.Value);
                if (string.IsNullOrWhiteSpace(raw))
                    return Ok(Envelope(EmptyGraph, "Rules fetched."));
                using var doc = JsonDocument.Parse(raw);
                return Ok(Envelope(doc.RootElement.Clone(), "Rules fetched."));
            }, projectId, subProjectId);

        [HttpPut]
        public Task<IActionResult> Save([FromQuery] long? projectId, [FromQuery] long? subProjectId, [FromQuery] long? yardTypeId, [FromBody] JsonElement body)
            => Run(async schema =>
            {
                if (yardTypeId is not > 0)
                    return BadRequest(Envelope<JsonElement?>(null, "Open a yard, then save the rules for that yard."));
                if (body.ValueKind != JsonValueKind.Object
                    || !body.TryGetProperty("nodes", out var nodes) || nodes.ValueKind != JsonValueKind.Array
                    || !body.TryGetProperty("edges", out var edges) || edges.ValueKind != JsonValueKind.Array)
                    return BadRequest(Envelope<JsonElement?>(null, "A decision graph needs nodes and edges."));

                await _rules.UpsertGraphAsync(schema, yardTypeId.Value, body.GetRawText(), Schema.CurrentUserId());
                return Ok(Envelope(true, "Rules saved"));
            }, projectId, subProjectId);
    }
}
