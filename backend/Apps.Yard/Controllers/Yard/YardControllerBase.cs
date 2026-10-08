using Microsoft.AspNetCore.Mvc;
using Primworth_API.Models.Yard;
using Primworth_API.Repository.Yard;
using Primworth_API.ViewModels;

namespace Primworth_API.Controllers.Yard
{
    /// <summary>
    /// What every Yard controller does the same way, written once.
    ///
    /// The facility comes from the schema context (headers, or ?projectId=&amp;subProjectId= to
    /// override). Failures are real status codes with the platform envelope in the body: 404 when
    /// the module is not enabled here or the row is gone, 400 when the yard refuses in words,
    /// 409 on a duplicate, 500 for anything else, whose text goes to the log and not the client.
    /// </summary>
    public abstract class YardControllerBase : ControllerBase
    {
        protected readonly IYardSchemaContext Schema;
        private readonly ILogger _logger;

        protected YardControllerBase(IYardSchemaContext schema, ILogger logger)
        {
            Schema = schema;
            _logger = logger;
        }

        protected async Task<IActionResult> Run(Func<string, Task<IActionResult>> action, long? projectId, long? subProjectId)
        {
            try
            {
                var schema = await Schema.RequireAsync(Schema.ProjectIdOrHeader(projectId), Schema.SubProjectIdOrHeader(subProjectId));
                return await action(schema);
            }
            catch (YardNotEnabledException ex) { return NotFound(Envelope<object?>(null, ex.Message)); }
            catch (YardRuleException ex) { return BadRequest(Envelope<object?>(null, ex.Message)); }
            catch (YardDuplicateException ex) { return Conflict(Envelope<object?>(null, ex.Message)); }
            catch (Exception ex)
            {
                _logger.LogError(ex, "Yard request failed: {Method} {Path}", Request.Method, Request.Path);
                return StatusCode(StatusCodes.Status500InternalServerError,
                    Envelope<object?>(null, "The request could not be completed. The error has been logged."));
            }
        }

        protected static ServiceResponse<T> Envelope<T>(T data, string message)
            => new() { Data = data, Message = message, Success = data != null };
    }
}
