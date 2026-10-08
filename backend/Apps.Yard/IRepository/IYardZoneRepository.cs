using Primworth_API.Models.Yard;

namespace Primworth_API.IRepository
{
    /// <summary>Areas, positions and material types. Every method takes the already-resolved facility schema.</summary>
    public interface IYardZoneRepository
    {
        Task<IEnumerable<YardMaterialType>> GetMaterialTypesAsync(string schema, bool includeInactive);
        Task<IEnumerable<YardZone>> GetZonesAsync(string schema);
        Task<IEnumerable<YardPosition>> GetPositionsAsync(string schema, long zoneId);
        Task<long> CreateZoneAsync(string schema, YardZoneInput input, long userId);
        Task<bool> UpdatePositionCapacityAsync(string schema, long positionId, int capacityLimit, long userId);
        Task<bool> DeleteZoneAsync(string schema, long zoneId, long userId);
        Task<IReadOnlyDictionary<string, string>> GetConfigAsync(string schema);
        Task SetConfigAsync(string schema, string key, string? value, long userId);
    }
}
