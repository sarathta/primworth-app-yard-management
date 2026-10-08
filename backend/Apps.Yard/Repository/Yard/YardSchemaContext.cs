using System.Security.Claims;
using Dapper;
using Microsoft.AspNetCore.Http;
using Primworth_API.IRepository;
using Primworth_API.Queries.Yard;
using Primworth_API.Services.Permissions;

namespace Primworth_API.Repository.Yard
{
    /// <summary>
    /// The one place Yard Management learns which facility a request is for.
    ///
    /// A yard belongs to one facility, and a facility is a sub-project, so this module is
    /// sub-project scoped: no sub-project selected means no schema, not "fall back to the
    /// project". The tenant comes from the X-Project-Id / X-Sub-Project-Id headers the platform's
    /// API client sends on every call (or ?projectId=&amp;subProjectId= to override). Controllers and
    /// repositories never read headers or claims themselves; they ask this.
    ///
    /// Every schema it resolves is first checked against the caller's project mapping through
    /// IProjectAccessGuard, so a user from another plant gets 403. Keep that call first in any
    /// resolve method you add.
    /// </summary>
    public interface IYardSchemaContext
    {
        long? ProjectIdOrHeader(long? explicitProjectId);
        long? SubProjectIdOrHeader(long? explicitSubProjectId);

        /// <summary>The facility schema if the module is provisioned there; null when not enabled.</summary>
        Task<string?> ResolveAsync(long? projectId, long? subProjectId);

        /// <summary>As ResolveAsync, but throws a clear message when the module is not enabled.</summary>
        Task<string> RequireAsync(long? projectId, long? subProjectId);

        /// <summary>sys_users.id from the bearer token; 0 when unauthenticated.</summary>
        long CurrentUserId();
    }

    public class YardSchemaContext : IYardSchemaContext
    {
        private readonly DapperContext _context;
        private readonly ISchemaResolverService _schemaResolver;
        private readonly IHttpContextAccessor _httpContextAccessor;
        private readonly IProjectAccessGuard _access;

        public YardSchemaContext(
            DapperContext context,
            ISchemaResolverService schemaResolver,
            IHttpContextAccessor httpContextAccessor,
            IProjectAccessGuard access)
        {
            _context = context;
            _schemaResolver = schemaResolver;
            _httpContextAccessor = httpContextAccessor;
            _access = access;
        }

        public long? ProjectIdOrHeader(long? explicitProjectId)
            => explicitProjectId is > 0 ? explicitProjectId : Header("X-Project-Id");

        public long? SubProjectIdOrHeader(long? explicitSubProjectId)
            => explicitSubProjectId is > 0 ? explicitSubProjectId : Header("X-Sub-Project-Id");

        public async Task<string?> ResolveAsync(long? projectId, long? subProjectId)
        {
            if (projectId is not > 0 || subProjectId is not > 0) return null;

            await _access.EnsureMayEnterAsync(projectId, subProjectId);

            var schema = await _schemaResolver.ResolveSubProjectSchema(projectId, subProjectId);
            if (string.IsNullOrWhiteSpace(schema)) return null;

            using var connection = _context.CreateConnection();
            var provisioned = await connection.ExecuteScalarAsync<int>(
                YardDdl.ModuleTableExists, new { schemaName = schema });

            return provisioned > 0 ? schema : null;
        }

        public async Task<string> RequireAsync(long? projectId, long? subProjectId)
            => await ResolveAsync(projectId, subProjectId)
               ?? throw new YardNotEnabledException(
                   "Yard Management is not enabled for this facility. Select a sub project and map the application to it first.");

        public long CurrentUserId()
        {
            var raw = _httpContextAccessor.HttpContext?.User.FindFirstValue(ClaimTypes.NameIdentifier);
            return long.TryParse(raw, out var id) ? id : 0;
        }

        private long? Header(string name)
        {
            var raw = _httpContextAccessor.HttpContext?.Request.Headers[name].ToString();
            return long.TryParse(raw, out var id) && id > 0 ? id : null;
        }
    }

    /// <summary>Raised when a request names a facility the module is not mapped to. Controllers answer 404.</summary>
    public class YardNotEnabledException : Exception
    {
        public YardNotEnabledException(string message) : base(message) { }
    }
}
