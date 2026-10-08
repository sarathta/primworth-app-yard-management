using System.Text.Json;
using Microsoft.AspNetCore.Mvc;
using Primworth_API.Models.Yard;
using Primworth_API.Repository.Yard;

namespace Primworth_API.Controllers.Yard
{
    /// <summary>
    /// What the compat controllers share: tenant resolution, the error shape the screens read,
    /// and the Prisma-style projections.
    ///
    /// The embedded mSYMS screens are used as they were. They call the paths and expect the JSON
    /// shapes their original Express/Prisma backend produced: ids as strings, nested slab / zone /
    /// column objects, raw JSON rather than the platform envelope, and `{ error }` on a 4xx. This
    /// layer answers in that dialect; the data and the rules come from the ordinary repositories,
    /// so every write runs through the same placement and the same stack invariant as the native API.
    /// </summary>
    public abstract class YardCompatBase : ControllerBase
    {
        // Permission codes, named so the attributes read as sentences.
        protected const string Dashboard = "YRDD";
        protected const string Operator = "YRDO";
        protected const string Quality = "YRDQ";
        protected const string ReleaseQa = "YRDU";
        protected const string Admin = "YRDA";
        protected const string Mill = "YRDH";

        protected readonly IYardSchemaContext Schema;
        private readonly ILogger _logger;

        protected YardCompatBase(IYardSchemaContext schema, ILogger logger)
        {
            Schema = schema;
            _logger = logger;
        }

        /// <summary>Resolves the facility and runs the action. Refusals come back as `{ error }` the screens show verbatim.</summary>
        protected async Task<IActionResult> Run(Func<string, Task<IActionResult>> action, long? projectId, long? subProjectId)
        {
            try
            {
                var schema = await Schema.RequireAsync(Schema.ProjectIdOrHeader(projectId), Schema.SubProjectIdOrHeader(subProjectId));
                return await action(schema);
            }
            catch (YardNotEnabledException ex) { return NotFound(new { error = ex.Message }); }
            catch (YardRuleException ex) { return BadRequest(new { error = ex.Message }); }
            catch (YardDuplicateException ex) { return BadRequest(new { error = ex.Message }); }
            catch (Exception ex)
            {
                _logger.LogError(ex, "Yard compat request failed: {Method} {Path}", Request.Method, Request.Path);
                return StatusCode(StatusCodes.Status500InternalServerError, new { error = "The request could not be completed. The error has been logged." });
            }
        }

        /// <summary>Reads: an unprovisioned facility answers an empty list, which the screens render as empty.</summary>
        protected async Task<IActionResult> RunRead(Func<string, Task<IActionResult>> action, long? projectId, long? subProjectId, object empty)
        {
            var schema = await Schema.ResolveAsync(Schema.ProjectIdOrHeader(projectId), Schema.SubProjectIdOrHeader(subProjectId));
            if (schema == null) return Ok(empty);
            return await Run(action, projectId, subProjectId);
        }

        // ── Vocabulary the screens use ───────────────────────────────────────────
        // The screens were written for a slab yard: an area purpose is a "zone type", and the
        // rework purpose is spelt SCARFING. Translated at the edge, both ways.
        protected static string ToScreenType(string? purpose) => purpose == YardZonePurpose.Rework ? "SCARFING" : purpose ?? YardZonePurpose.Storage;
        protected static string FromScreenType(string? type) => string.Equals(type, "SCARFING", StringComparison.OrdinalIgnoreCase)
            ? YardZonePurpose.Rework : (type ?? YardZonePurpose.Storage).Trim().ToUpperInvariant();

        // ── Shapes ───────────────────────────────────────────────────────────────
        protected static object SlabShape(YardCompatItemRow s) => new
        {
            id = s.item_id.ToString(),
            slab_code = s.item_code,
            heat_number = s.heat_number,
            stack_index = s.stack_index,
            status = s.item_status,
            quality_status = s.quality_status,
            grade_code = s.grade_code,
            temperature_state = s.temperature_state,
            stale_flagged = false,
            chemistry_data = (object?)null,
            cast_time = (object?)null,
            target_reheat_temp = (object?)null,
            rolling_width = (object?)null,
        };

        protected static object FullSlabShape(YardCompatItem s)
        {
            var attrs = Json(s.attributes) as JsonElement?;
            return new
            {
                id = s.item_id.ToString(),
                slab_code = s.item_code,
                heat_number = s.heat_number,
                stack_index = s.stack_index,
                status = s.item_status,
                quality_status = s.quality_status,
                grade_code = s.grade_code,
                temperature_state = s.temperature_state,
                stale_flagged = false,
                cast_time = Attr(attrs, "cast_time"),
                target_reheat_temp = Attr(attrs, "target_reheat_c"),
                rolling_width = Attr(attrs, "rolling_width_mm"),
                weight_kg = s.weight_kg,
                chemistry_data = s.grade_code == null ? null : new { grade = s.grade_code },
                // The screens read dimensions.length / width / thickness.
                dimensions = new { length = Attr(attrs, "length_mm"), width = Attr(attrs, "width_mm"), thickness = Attr(attrs, "thickness_mm") },
                attributes = attrs,
                material_type = s.type_key,
                updated_at = s.updated_on,
                zone_id = s.zone_id?.ToString(),
                column_id = s.position_id?.ToString(),
                zone = s.zone_id.HasValue ? new { id = s.zone_id.Value.ToString(), name = s.zone_name, type = ToScreenType(s.zone_purpose) } : null,
                column = Column(s.position_id, s.row_index, s.col_index),
            };
        }

        protected static object MovementShape(YardMovement l) => new
        {
            id = l.movement_log_id.ToString(),
            slab_id = l.item_id.ToString(),
            slab = new { id = l.item_id.ToString(), slab_code = l.item_code, heat_number = l.heat_number },
            from_zone_name = l.from_zone_name,
            to_zone_name = l.to_zone_name,
            from_col_desc = l.from_position,
            to_col_desc = l.to_position,
            order_type = l.order_type,
            performed_name = l.performed_name,
            timestamp = l.log_timestamp,
        };

        protected static object? Zone(long? id, string? name, string? purpose)
            => id.HasValue ? new { id = id.Value.ToString(), name, type = ToScreenType(purpose) } : null;

        protected static object? Column(long? id, int? row, int? col)
            => id.HasValue ? new { id = id.Value.ToString(), row_index = row, col_index = col } : null;

        /// <summary>A jsonb column handed back as JSON, not as a string containing JSON.</summary>
        protected static object? Json(string? raw)
        {
            if (string.IsNullOrWhiteSpace(raw)) return null;
            try { return JsonDocument.Parse(raw).RootElement.Clone(); }
            catch (JsonException) { return raw; }
        }

        private static object? Attr(JsonElement? attrs, string key)
        {
            if (attrs is not { ValueKind: JsonValueKind.Object } o || !o.TryGetProperty(key, out var v)) return null;
            return v.ValueKind switch
            {
                JsonValueKind.Number => v.TryGetDecimal(out var d) ? d : null,
                JsonValueKind.String => v.GetString(),
                JsonValueKind.True => true,
                JsonValueKind.False => false,
                _ => null,
            };
        }

        protected static bool Decimal(string? raw, out decimal value)
            => decimal.TryParse(raw, System.Globalization.NumberStyles.Any, System.Globalization.CultureInfo.InvariantCulture, out value);
    }
}
