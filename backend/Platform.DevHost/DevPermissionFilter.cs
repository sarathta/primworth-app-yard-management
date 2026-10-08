using Microsoft.AspNetCore.Mvc;
using Microsoft.AspNetCore.Mvc.Filters;
using Primworth_API.Services.Permissions;
using Primworth_API.ViewModels;

namespace Platform.DevHost
{
    /// <summary>
    /// Enforces [RequiresPermission] the way the platform does, against the dev user's permission
    /// list. Controller-level and action-level codes are all required. "*" passes everything.
    /// </summary>
    public class DevPermissionFilter : IAsyncActionFilter
    {
        private readonly DevOptions _dev;

        public DevPermissionFilter(DevOptions dev) => _dev = dev;

        public Task OnActionExecutionAsync(ActionExecutingContext context, ActionExecutionDelegate next)
        {
            var required = context.ActionDescriptor.EndpointMetadata
                .OfType<RequiresPermissionAttribute>()
                .SelectMany(a => a.Codes)
                .Distinct(StringComparer.OrdinalIgnoreCase)
                .ToList();

            if (required.Count == 0 || _dev.Permissions.Contains("*"))
                return next();

            var missing = required.Where(c => !_dev.Permissions.Contains(c, StringComparer.OrdinalIgnoreCase)).ToList();
            if (missing.Count == 0)
                return next();

            context.Result = new ObjectResult(new ServiceResponse<object?>
            {
                Data = null,
                Success = false,
                Message = $"Missing permission: {string.Join(", ", missing)}. Grant it in Dev:Permissions to test this endpoint.",
            })
            { StatusCode = StatusCodes.Status403Forbidden };
            return Task.CompletedTask;
        }
    }
}
