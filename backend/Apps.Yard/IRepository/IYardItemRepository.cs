using Primworth_API.Models.Yard;

namespace Primworth_API.IRepository
{
    public class YardItemQuery
    {
        public string? itemStatus { get; set; }
        public string? qualityStatus { get; set; }
        public long? zoneId { get; set; }
        public string? search { get; set; }
        public int page { get; set; } = 1;
        public int pageSize { get; set; } = 100;
    }

    /// <summary>Items. Every method takes the already-resolved facility schema.</summary>
    public interface IYardItemRepository
    {
        Task<IEnumerable<YardItem>> GetItemsAsync(string schema, YardItemQuery query);
        Task<YardItem?> GetItemByIdAsync(string schema, long itemId);

        /// <summary>Validates the attributes against the material type, inserts, and (by default) allocates.</summary>
        Task<YardAllocationResult> CreateItemAsync(string schema, YardItemInput input, long userId);

        Task<bool> SetQualityAsync(string schema, long itemId, string qualityStatus, long userId);

        /// <summary>Ship or scrap: the item leaves the yard, the gap it leaves is closed, the log gets a line.</summary>
        Task<bool> RemoveFromYardAsync(string schema, long itemId, string newStatus, string orderType, long userId);
    }
}
