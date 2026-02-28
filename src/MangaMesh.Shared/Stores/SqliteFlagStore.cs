using System.Text.Json;
using MangaMesh.Shared.Data;
using MangaMesh.Shared.Data.Entities;
using Microsoft.EntityFrameworkCore;

namespace MangaMesh.Shared.Stores
{
    public class SqliteFlagStore : IFlagStore
    {
        private readonly IndexDbContext _db;

        public SqliteFlagStore(IndexDbContext db)
        {
            _db = db;
        }

        public async Task AddAsync(ChapterFlagRecord flag)
        {
            _db.ChapterFlags.Add(new ChapterFlagEntity
            {
                Id = flag.Id,
                ManifestHash = flag.ManifestHash,
                SeriesId = flag.SeriesId,
                ChapterId = flag.ChapterId,
                Categories = JsonSerializer.Serialize(flag.Categories),
                Comment = flag.Comment,
                SubmittedUtc = flag.SubmittedUtc,
                Dismissed = flag.Dismissed
            });
            await _db.SaveChangesAsync();
        }

        public async Task<IEnumerable<ChapterFlagRecord>> GetByManifestHashAsync(string manifestHash)
        {
            var entities = await _db.ChapterFlags
                .AsNoTracking()
                .Where(f => f.ManifestHash == manifestHash)
                .OrderByDescending(f => f.SubmittedUtc)
                .ToListAsync();

            return entities.Select(ToRecord);
        }

        public async Task<IEnumerable<ChapterFlagRecord>> GetAllAsync(bool includeDismissed = false)
        {
            var query = _db.ChapterFlags.AsNoTracking();
            if (!includeDismissed)
                query = query.Where(f => !f.Dismissed);

            var entities = await query
                .OrderByDescending(f => f.SubmittedUtc)
                .ToListAsync();

            return entities.Select(ToRecord);
        }

        public async Task<FlagSummaryData> GetSummaryAsync(string manifestHash)
        {
            var entities = await _db.ChapterFlags
                .AsNoTracking()
                .Where(f => f.ManifestHash == manifestHash && !f.Dismissed)
                .ToListAsync();

            return ComputeSummary(manifestHash, entities);
        }

        public async Task<Dictionary<string, FlagSummaryData>> GetSummariesAsync(IEnumerable<string> manifestHashes)
        {
            var hashList = manifestHashes.ToList();
            var entities = await _db.ChapterFlags
                .AsNoTracking()
                .Where(f => hashList.Contains(f.ManifestHash) && !f.Dismissed)
                .ToListAsync();

            return entities
                .GroupBy(f => f.ManifestHash)
                .ToDictionary(
                    g => g.Key,
                    g => ComputeSummary(g.Key, g.ToList())
                );
        }

        public async Task DismissAsync(string id)
        {
            var entity = await _db.ChapterFlags.FindAsync(id);
            if (entity != null)
            {
                entity.Dismissed = true;
                await _db.SaveChangesAsync();
            }
        }

        public async Task DeleteAsync(string id)
        {
            var entity = await _db.ChapterFlags.FindAsync(id);
            if (entity != null)
            {
                _db.ChapterFlags.Remove(entity);
                await _db.SaveChangesAsync();
            }
        }

        private static ChapterFlagRecord ToRecord(ChapterFlagEntity e) =>
            new(
                e.Id,
                e.ManifestHash,
                e.SeriesId,
                e.ChapterId,
                JsonSerializer.Deserialize<List<string>>(e.Categories) ?? [],
                e.Comment,
                e.SubmittedUtc,
                e.Dismissed
            );

        private static FlagSummaryData ComputeSummary(string manifestHash, IList<ChapterFlagEntity> flags)
        {
            var categoryCounts = new Dictionary<string, int>();
            foreach (var flag in flags)
            {
                var cats = JsonSerializer.Deserialize<List<string>>(flag.Categories) ?? [];
                foreach (var cat in cats)
                    categoryCounts[cat] = categoryCounts.GetValueOrDefault(cat) + 1;
            }

            var topCategories = categoryCounts
                .OrderByDescending(kvp => kvp.Value)
                .Take(3)
                .Select(kvp => kvp.Key)
                .ToList();

            return new FlagSummaryData(manifestHash, flags.Count, flags.Count >= 2, topCategories);
        }
    }
}
