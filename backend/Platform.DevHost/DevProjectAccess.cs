using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Mvc;
using Microsoft.AspNetCore.Mvc.Filters;
using Primworth_API.Services.Permissions;
using Primworth_API.ViewModels;

namespace Platform.DevHost
{
    /// <summary>
    /// Membership the way the platform checks it, against <c>Dev:Projects</c> instead of User
    /// Management: "*" is everywhere, "7" is all of project 7, "7/3" is only its sub-project 3.
    /// Like the platform, a request with no HTTP context or to an [AllowAnonymous] endpoint is
    /// not checked, and a refusal marks the request so <see cref="DevProjectAccessDeniedFilter"/>
    /// answers 403 whatever the controller's catch-all returned.
    /// </summary>
    public class DevProjectAccessGuard : IProjectAccessGuard
    {
        internal const string DeniedItemKey = "Primworth.ProjectAccessDenied";

        private readonly DevOptions _dev;
        private readonly IHttpContextAccessor _http;

        public DevProjectAccessGuard(DevOptions dev, IHttpContextAccessor http)
        {
            _dev = dev;
            _http = http;
        }

        public Task EnsureMayEnterAsync(long? projectId, long? subProjectId, CancellationToken ct = default)
        {
            if (projectId is not > 0) return Task.CompletedTask;

            var context = _http.HttpContext;
            if (context is null) return Task.CompletedTask;
            if (context.GetEndpoint()?.Metadata.GetMetadata<IAllowAnonymous>() is not null) return Task.CompletedTask;

            if (IsMapped(projectId.Value, subProjectId is > 0 ? subProjectId : null)) return Task.CompletedTask;

            var message = $"The dev user is not mapped to project {projectId}" +
                          (subProjectId is > 0 ? $" / sub-project {subProjectId}" : "") +
                          ". Add it to Dev:Projects to test this request.";
            context.Items[DeniedItemKey] = message;
            throw new ProjectAccessDeniedException(message);
        }

        private bool IsMapped(long projectId, long? subProjectId)
        {
            foreach (var entry in _dev.Projects ?? new[] { "*" })
            {
                var value = entry.Trim();
                if (value == "*") return true;

                var parts = value.Split('/');
                if (!long.TryParse(parts[0], out var mappedProject) || mappedProject != projectId) continue;

                // "7" maps the whole project; "7/3" maps one sub-project, and counts for a
                // project-only question too, as a sub-project row does in the platform.
                if (parts.Length == 1 || subProjectId is null) return true;
                if (long.TryParse(parts[1], out var mappedSub) && mappedSub == subProjectId) return true;
            }
            return false;
        }
    }

    /// <summary>Answers a membership refusal with 403, as the platform's filter does.</summary>
    public class DevProjectAccessDeniedFilter : IAsyncAlwaysRunResultFilter, IExceptionFilter
    {
        public void OnException(ExceptionContext context)
        {
            if (context.Exception is not ProjectAccessDeniedException denied) return;
            context.Result = Forbidden(denied.Message);
            context.ExceptionHandled = true;
        }

        public Task OnResultExecutionAsync(ResultExecutingContext context, ResultExecutionDelegate next)
        {
            if (context.HttpContext.Items.TryGetValue(DevProjectAccessGuard.DeniedItemKey, out var message))
                context.Result = Forbidden(message as string ?? "Access denied.");
            return next();
        }

        private static ObjectResult Forbidden(string message) =>
            new(new ServiceResponse<object?> { Data = null, Success = false, Message = message })
            { StatusCode = StatusCodes.Status403Forbidden };
    }
}
