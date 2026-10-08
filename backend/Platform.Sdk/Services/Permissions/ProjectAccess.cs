namespace Primworth_API.Services.Permissions
{
    /// <summary>
    /// Tenant membership: may the signed-in caller use the project (and sub-project) they named?
    ///
    /// The project id comes from the browser — a query value or the X-Project-Id header — so a
    /// user whose role holds the app's permission could otherwise name another plant's id. Your
    /// SchemaContext calls this before it resolves a schema; nothing else in the app needs to.
    ///
    /// In the platform the answer is the user-to-project mapping from User Management (a row for the
    /// whole project, or for that sub-project; super admins pass), and a refusal is answered 403.
    /// In the dev host it is <c>Dev:Projects</c> in appsettings ("*" maps the dev user everywhere).
    /// Background work (no HTTP request) and [AllowAnonymous] endpoints are not checked.
    /// </summary>
    public interface IProjectAccessGuard
    {
        /// <summary>
        /// Throws <see cref="ProjectAccessDeniedException"/> when the caller is not mapped to the
        /// project, or to the sub-project when one is given. Does nothing when projectId is missing.
        /// </summary>
        Task EnsureMayEnterAsync(long? projectId, long? subProjectId, CancellationToken ct = default);
    }

    /// <summary>What <see cref="IProjectAccessGuard"/> throws. The host answers it with 403.</summary>
    public sealed class ProjectAccessDeniedException : Exception
    {
        public ProjectAccessDeniedException(string message) : base(message) { }
    }
}
