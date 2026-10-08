using System.Data;
using Primworth_API.Models.Yard;

namespace Primworth_API.IRepository
{
    /// <summary>Size profiles, rework activities, product mixes, the grade list and the activity log.</summary>
    public interface IYardMasterRepository
    {
        Task<IEnumerable<YardGradeOption>> GetGradesAsync(string schema);

        Task<IEnumerable<YardSizeProfile>> GetSizeProfilesAsync(string schema);
        Task<long> SaveSizeProfileAsync(string schema, long? id, YardSizeProfileInput input, long userId);
        Task<bool> DeleteSizeProfileAsync(string schema, long id, long userId);

        Task<IEnumerable<YardReworkActivity>> GetReworkActivitiesAsync(string schema);
        Task<long> SaveReworkActivityAsync(string schema, long? id, YardReworkActivityInput input, long userId);
        Task<bool> DeleteReworkActivityAsync(string schema, long id, long userId);

        Task<IEnumerable<YardProductMix>> GetProductMixesAsync(string schema);
        Task<long> SaveProductMixAsync(string schema, long id, YardProductMixInput input, long userId);
        Task<bool> DeleteProductMixAsync(string schema, long id, long userId);

        Task<(IEnumerable<YardSystemLogRow> rows, long total)> GetSystemLogsAsync(string schema, int page, int limit, string? action, DateTime? from, DateTime? to);
    }
}
