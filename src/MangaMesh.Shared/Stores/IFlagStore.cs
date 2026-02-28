namespace MangaMesh.Shared.Stores
{
    public record ChapterFlagRecord(
        string Id,
        string ManifestHash,
        string SeriesId,
        string ChapterId,
        List<string> Categories,
        string? Comment,
        DateTime SubmittedUtc,
        bool Dismissed
    );

    public record FlagSummaryData(
        string ManifestHash,
        int TotalFlags,
        bool HasMultiplePeerFlags,
        List<string> TopCategories
    );

    public interface IFlagStore
    {
        Task AddAsync(ChapterFlagRecord flag);

        Task<IEnumerable<ChapterFlagRecord>> GetByManifestHashAsync(string manifestHash);

        Task<IEnumerable<ChapterFlagRecord>> GetAllAsync(bool includeDismissed = false);

        Task<FlagSummaryData> GetSummaryAsync(string manifestHash);

        Task<Dictionary<string, FlagSummaryData>> GetSummariesAsync(IEnumerable<string> manifestHashes);

        Task DismissAsync(string id);

        Task DeleteAsync(string id);
    }
}
