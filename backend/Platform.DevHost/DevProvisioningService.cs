using System.Data;
using Dapper;
using Primworth_API.IRepository;
using Primworth_API.Repository;
// System.Data also defines a SchemaType; we always mean the platform's Project/SubProject one.
using SchemaType = Primworth_API.Repository.SchemaType;

namespace Platform.DevHost
{
    /// <summary>
    /// Runs every registered provisioner against the sample projects — project-scoped ones into each
    /// project schema, sub-project-scoped ones into each sub-project schema — inside a transaction
    /// per run, exactly as the platform does when an application is mapped.
    /// </summary>
    public class DevProvisioningService
    {
        private readonly DapperContext _context;
        private readonly IEnumerable<IApplicationProvisioner> _provisioners;
        private readonly DevOptions _dev;

        public DevProvisioningService(DapperContext context, IEnumerable<IApplicationProvisioner> provisioners, DevOptions dev)
        {
            _context = context;
            _provisioners = provisioners;
            _dev = dev;
        }

        public async Task<List<string>> RunAllAsync(ILogger logger)
        {
            var results = new List<string>();
            var provisioners = _provisioners.ToList();
            if (provisioners.Count == 0)
            {
                logger.LogWarning("[DevHost] no IApplicationProvisioner registered — nothing to provision.");
                return results;
            }

            using var connection = _context.CreateConnection();
            connection.Open();

            var projects = (await connection.QueryAsync<(long project_id, string project_name)>(
                "SELECT project_id, project_name FROM public.sys_pr_project ORDER BY project_id")).ToList();

            foreach (var (projectId, projectName) in projects)
            {
                var projectSchema = DevSchemaNames.Project(projectId, projectName);
                await EnsureSchemaAsync(connection, projectSchema);

                foreach (var p in provisioners.Where(p => p.Scope is ApplicationScopeTarget.Project or ApplicationScopeTarget.Both))
                    results.Add(await RunOneAsync(connection, p, projectSchema, SchemaType.Project, projectId, null, null, logger));

                var subProjects = (await connection.QueryAsync<(long sub_project_id, string sub_project_name, long? facility_type_id)>(
                    $"SELECT sub_project_id, sub_project_name, facility_type_id FROM \"{projectSchema}\".sys_sp_sub_project ORDER BY sub_project_id")).ToList();

                foreach (var (subId, subName, facilityTypeId) in subProjects)
                {
                    var subSchema = DevSchemaNames.SubProject(projectId, projectName, subId, subName);
                    await EnsureSchemaAsync(connection, subSchema);

                    foreach (var p in provisioners.Where(p => p.Scope is ApplicationScopeTarget.SubProject or ApplicationScopeTarget.Both))
                        results.Add(await RunOneAsync(connection, p, subSchema, SchemaType.SubProject, projectId, subId, facilityTypeId, logger));
                }
            }
            return results;
        }

        private async Task<string> RunOneAsync(IDbConnection connection, IApplicationProvisioner p, string schema,
            SchemaType type, long projectId, long? subProjectId, long? facilityTypeId, ILogger logger)
        {
            using var tx = connection.BeginTransaction();
            try
            {
                await p.ProvisionAsync(new ApplicationProvisionContext
                {
                    Connection = connection,
                    Transaction = tx,
                    SchemaName = schema,
                    SchemaType = type,
                    ProjectId = projectId,
                    SubProjectId = subProjectId,
                    FacilityTypeId = facilityTypeId,
                    UserId = _dev.User.Id,
                });
                tx.Commit();
                var msg = $"provisioned '{p.ProvisionerKey}' into {schema}";
                logger.LogInformation("[DevHost] {Msg}", msg);
                return msg;
            }
            catch (Exception ex)
            {
                tx.Rollback();
                var msg = $"FAILED '{p.ProvisionerKey}' into {schema}: {ex.Message}";
                logger.LogError(ex, "[DevHost] {Msg}", msg);
                return msg;
            }
        }

        private static Task EnsureSchemaAsync(IDbConnection connection, string schema)
            => connection.ExecuteAsync($"CREATE SCHEMA IF NOT EXISTS \"{schema}\"");
    }
}
