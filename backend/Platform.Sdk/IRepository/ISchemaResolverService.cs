namespace Primworth_API.IRepository
{
    /// <summary>
    /// Turns platform ids into the physical schema a request must use. The platform implements
    /// this against its routing tables; the dev host implements it against the sample database.
    /// </summary>
    public interface ISchemaResolverService
    {
        /// <summary>The sub-project's schema, e.g. "79_rajuri_steel_1_sms_1_db".</summary>
        Task<string?> ResolveSubProjectSchema(long? projectId, long? subProjectId);

        /// <summary>
        /// The schema for a request: the sub-project's when a sub-project is named, otherwise the
        /// project's, e.g. "79_rajuri_steel_db".
        /// </summary>
        Task<string?> ResolveSchema(long? projectId, long? subProjectId);
    }
}
