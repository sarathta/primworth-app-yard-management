using System.Data;

namespace Primworth_API.IRepository
{
    public interface IYardRulesRepository
    {
        Task<string?> GetGraphAsync(string schema, long yardTypeId);
        Task<string?> GetGraphAsync(IDbConnection connection, IDbTransaction transaction, string schema, long yardTypeId);
        Task UpsertGraphAsync(string schema, long yardTypeId, string graphJson, long userId);
    }
}
