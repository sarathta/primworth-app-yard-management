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

            if (required.Count == 0)
                return next();

            // A signed-in account carries its own permission claims. Tokens issued before that
            // (the original dev sign-in) fall back to Dev:Permissions.
            var fromToken = context.HttpContext.User.FindAll("permission").Select(c => c.Value).ToList();
            IEnumerable<string> granted = fromToken.Count > 0 ? fromToken : _dev.Permissions;
            if (granted.Contains("*", StringComparer.OrdinalIgnoreCase))
                return next();

            var missing = required.Where(c => !granted.Contains(c, StringComparer.OrdinalIgnoreCase)).ToList();
            if (missing.Count == 0)
                return next();

            context.Result = new ObjectResult(new ServiceResponse<object?>
            {
                Data = null,
                Success = false,
                Message = $"You do not have permission to do that ({string.Join(", ", missing)}).",
            })
            { StatusCode = StatusCodes.Status403Forbidden };
            return Task.CompletedTask;
        }
    }
}
