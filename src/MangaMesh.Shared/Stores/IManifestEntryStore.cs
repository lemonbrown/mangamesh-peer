using MangaMesh.Shared.Models;

namespace MangaMesh.Shared.Stores
{
    public interface IManifestEntryStore
    {
        Task AddAsync(ManifestEntry entry);
        /// <summary>Returns only non-quarantined entries (used by the public API).</summary>
        Task<IEnumerable<ManifestEntry>> GetAllAsync();
        /// <summary>Returns all entries including quarantined ones (used by the admin API).</summary>
        Task<IEnumerable<ManifestEntry>> GetAllIncludingQuarantinedAsync();
        Task<ManifestEntry?> GetAsync(string hash);
        Task DeleteAsync(string hash);
        Task DeleteBySeriesIdAsync(string seriesId);
        Task SetQuarantineAsync(string hash, bool quarantined);
    }
}
