using System.Data;
using Primworth_API.Repository;
using SchemaType = Primworth_API.Repository.SchemaType;

namespace Primworth_API.IRepository
{
    /// <summary>Which schema types a provisioner wants to be run against.</summary>
    public enum ApplicationScopeTarget
    {
        Project,
        SubProject,
        Both
    }

    /// <summary>
    /// Everything a provisioner needs to create its tables in one tenant schema. The connection
    /// is supplied by the caller and is already inside the caller's transaction — a provisioner
    /// must never open its own.
    /// </summary>
    public class ApplicationProvisionContext
    {
        public required IDbConnection Connection { get; init; }
        public IDbTransaction? Transaction { get; init; }

        /// <summary>Target schema, already sanitised, e.g. "79_rajuri_steel_db".</summary>
        public required string SchemaName { get; init; }

        public SchemaType SchemaType { get; init; }
        public long? ProjectId { get; init; }
        public long? SubProjectId { get; init; }
        public long? FacilityTypeId { get; init; }

        /// <summary>sys_users.id of whoever triggered provisioning; goes into audit columns.</summary>
        public long UserId { get; init; }
    }

    /// <summary>
    /// An integrated application's hook for creating the tables it owns inside a tenant schema.
    /// IMPLEMENTATIONS MUST BE IDEMPOTENT: provisioning runs on create and on every later edit, so
    /// every statement is CREATE … IF NOT EXISTS / INSERT … WHERE NOT EXISTS. There is no
    /// deprovision hook; the platform never drops tenant tables.
    /// </summary>
    public interface IApplicationProvisioner
    {
        /// <summary>Matches public.application.provisioner_key.</summary>
        string ProvisionerKey { get; }

        ApplicationScopeTarget Scope { get; }

        Task ProvisionAsync(ApplicationProvisionContext ctx);

        /// <summary>How the application's tables behave in the Masters UI.</summary>
        IReadOnlyList<SchemaTableConfig> DescribeMastersUiRules();
    }
}
