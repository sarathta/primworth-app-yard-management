using Primworth_API.Models.Yard;

namespace Primworth_API.IRepository
{
    /// <summary>Reads in the shapes the embedded mSYMS screens expect. Writes go through the ordinary repositories.</summary>
    public interface IYardCompatRepository
    {
        Task<YardCompatOverview> GetOverviewAsync(string schema);
        Task<IEnumerable<YardCompatItem>> GetItemsAsync(string schema, string? qualityStatus);
        Task<YardCompatItem?> GetItemByIdAsync(string schema, long itemId);
        Task<IEnumerable<YardCompatItem>> GetDispatchCandidatesAsync(string schema);
        Task<IEnumerable<YardCompatItem>> GetMillCampaignAsync(string schema, int limit);
        Task<IEnumerable<YardCompatWorkOrderRow>> GetWorkOrdersAsync(string schema);
    }
}
