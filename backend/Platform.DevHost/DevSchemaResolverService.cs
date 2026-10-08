using System.Text.RegularExpressions;
using Dapper;
using Primworth_API.IRepository;
using Primworth_API.Repository;

namespace Platform.DevHost
{
    /// <summary>Schema naming used by the sample database: "{id}_{slug}_db", the platform's shape.</summary>
    public static class DevSchemaNames
    {
        public static string Slug(string name)
            => Regex.Replace(name.Trim().ToLowerInvariant(), "[^a-z0-9]+", "_").Trim('_');

        public static string Project(long projectId, string projectName)
            => $"{projectId}_{Slug(projectName)}_db";

        public static string SubProject(long projectId, string projectName, long subProjectId, string subProjectName)
            => $"{projectId}_{Slug(projectName)}_{subProjectId}_{Slug(subProjectName)}_db";
    }

    /// <summary>
    /// Resolves schemas from the sample database's routing tables: public.sys_pr_project for
    /// projects and the project schema's sys_sp_sub_project for sub-projects — the same lookups the
    /// platform makes.
    /// </summary>
    public class DevSchemaResolverService : ISchemaResolverService
    {
        private readonly DapperContext _context;

        public DevSchemaResolverService(DapperContext context) => _context = context;

        public async Task<string?> ResolveSchema(long? projectId, long? subProjectId)
        {
            if (subProjectId is > 0) return await ResolveSubProjectSchema(projectId, subProjectId);
            if (projectId is not > 0) return null;

            using var connection = _context.CreateConnection();
            var name = await connection.QueryFirstOrDefaultAsync<string>(
                "SELECT project_name FROM public.sys_pr_project WHERE project_id = @id", new { id = projectId });
            return name == null ? null : DevSchemaNames.Project(projectId.Value, name);
        }

        public async Task<string?> ResolveSubProjectSchema(long? projectId, long? subProjectId)
        {
            if (projectId is not > 0 || subProjectId is not > 0) return null;

            using var connection = _context.CreateConnection();
            var projectName = await connection.QueryFirstOrDefaultAsync<string>(
                "SELECT project_name FROM public.sys_pr_project WHERE project_id = @id", new { id = projectId });
            if (projectName == null) return null;

            var projectSchema = DevSchemaNames.Project(projectId.Value, projectName);
            var subName = await connection.QueryFirstOrDefaultAsync<string>(
                $"SELECT sub_project_name FROM \"{projectSchema}\".sys_sp_sub_project WHERE sub_project_id = @id",
                new { id = subProjectId });
            return subName == null ? null : DevSchemaNames.SubProject(projectId.Value, projectName, subProjectId.Value, subName);
        }
    }
}
