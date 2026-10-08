using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Mvc;
using Primworth_API.IRepository;
using Primworth_API.Models.Yard;
using Primworth_API.Repository.Yard;
using Primworth_API.Services.Permissions;

namespace Primworth_API.Controllers.Yard
{
    /// <summary>Items of the current facility: list, look up, ingest, hold and release.</summary>
    [ApiController]
    [Route("api/yard/items")]
    [Authorize]
    [RequiresPermission("YRDD", "YRDO", "YRDA")]
    public class YardItemController : YardControllerBase
    {
        private readonly IYardItemRepository _items;

        public YardItemController(IYardItemRepository items, IYardSchemaContext schema, ILogger<YardItemController> logger)
            : base(schema, logger) => _items = items;

        /// <summary>GET api/yard/items?itemStatus=&amp;qualityStatus=&amp;zoneId=&amp;search=&amp;page=&amp;pageSize=</summary>
        [HttpGet]
        public Task<IActionResult> GetItems([FromQuery] YardItemQuery query, [FromQuery] long? projectId, [FromQuery] long? subProjectId)
            => Run(async schema => Ok(Envelope(await _items.GetItemsAsync(schema, query), "Items fetched.")), projectId, subProjectId);

        [HttpGet("{itemId:long}")]
        public Task<IActionResult> GetItem(long itemId, [FromQuery] long? projectId, [FromQuery] long? subProjectId)
            => Run(async schema =>
            {
                var item = await _items.GetItemByIdAsync(schema, itemId);
                return item == null ? NotFound(Envelope<YardItem?>(null, "Item not found.")) : Ok(Envelope(item, "Item fetched."));
            }, projectId, subProjectId);

        /// <summary>
        /// POST api/yard/items — ingest an item and, by default, decide where it goes.
        /// This is the one entry point every feed uses: a webhook receiver maps its payload into
        /// a YardItemInput and calls the same repository method.
        /// </summary>
        [HttpPost]
        [RequiresPermission("YRDO", "YRDA")]
        public Task<IActionResult> CreateItem([FromBody] YardItemInput input, [FromQuery] long? projectId, [FromQuery] long? subProjectId)
            => Run(async schema =>
            {
                var problem = input.Validate();
                if (problem != null) return BadRequest(Envelope<YardAllocationResult?>(null, problem));

                var result = await _items.CreateItemAsync(schema, input, Schema.CurrentUserId());
                var message = result.allocated ? $"{input.item_code.Trim()} arrives at {result.destination}."
                            : result.held ? $"{input.item_code.Trim()} is on hold: {result.reason}"
                            : $"{input.item_code.Trim()} recorded.";
                return Ok(Envelope(result, message));
            }, projectId, subProjectId);

        /// <summary>PUT api/yard/items/{itemId}/hold — a quality hold; blocks every movement.</summary>
        [HttpPut("{itemId:long}/hold")]
        [RequiresPermission("YRDO", "YRDA")]
        public Task<IActionResult> Hold(long itemId, [FromQuery] long? projectId, [FromQuery] long? subProjectId)
            => Run(async schema => await _items.SetQualityAsync(schema, itemId, YardQualityStatus.LockedQa, Schema.CurrentUserId())
                ? Ok(Envelope(true, "Hold applied.")) : NotFound(Envelope<bool?>(null, "Item not found.")), projectId, subProjectId);

        /// <summary>PUT api/yard/items/{itemId}/release — only the admin code may clear a hold.</summary>
        [HttpPut("{itemId:long}/release")]
        [RequiresPermission("YRDA")]
        public Task<IActionResult> Release(long itemId, [FromQuery] long? projectId, [FromQuery] long? subProjectId)
            => Run(async schema => await _items.SetQualityAsync(schema, itemId, YardQualityStatus.Approved, Schema.CurrentUserId())
                ? Ok(Envelope(true, "Hold cleared. The item can move again.")) : NotFound(Envelope<bool?>(null, "Item not found.")), projectId, subProjectId);
    }
}
