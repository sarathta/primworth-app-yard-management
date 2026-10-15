using Primworth_API.Models.Yard;

namespace Primworth_API.IRepository
{
    /// <summary>Reads in the shapes the embedded mSYMS screens expect. Writes go through the ordinary repositories.</summary>
    public interface IYardCompatRepository
    {
        Task<YardCompatOverview> GetOverviewAsync(string schema, string? materialType = null, long? yardTypeId = null);
        Task<IEnumerable<YardCompatItem>> GetItemsAsync(string schema, string? qualityStatus, string? materialType = null, long? yardTypeId = null);
        Task<YardCompatItem?> GetItemByIdAsync(string schema, long itemId);
        Task<IEnumerable<YardCompatItem>> GetDispatchCandidatesAsync(string schema, string? materialType = null, long? yardTypeId = null);
        Task<IEnumerable<YardCompatItem>> GetMillCampaignAsync(string schema, int limit, string? materialType = null, long? yardTypeId = null);
        Task<IEnumerable<YardCompatWorkOrderRow>> GetWorkOrdersAsync(string schema, string? materialType = null, long? yardTypeId = null);
    }
}
