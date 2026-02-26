using System.Linq.Expressions;
using MangaMesh.Shared.Data;
using MangaMesh.Shared.Data.Entities;
using MangaMesh.Shared.Models;
using Microsoft.EntityFrameworkCore;

namespace MangaMesh.Shared.Stores
{
    public class SqliteManifestEntryStore : SqliteStoreBase<ManifestEntry, ManifestEntryEntity, string>, IManifestEntryStore
    {
        public SqliteManifestEntryStore(IndexDbContext db) : base(db)
        {
        }

        protected override DbSet<ManifestEntryEntity> GetDbSet() => Db.ManifestEntries;

        protected override string GetEntityKey(ManifestEntryEntity entity) => entity.ManifestHash;

        protected override Expression<Func<ManifestEntryEntity, bool>> GetKeyPredicate(string key)
            => e => e.ManifestHash == key;

        protected override ManifestEntry MapToModel(ManifestEntryEntity entity)
        {
            return new ManifestEntry
            {
                ManifestHash = entity.ManifestHash,
                SeriesId = entity.SeriesId,
                ChapterId = entity.ChapterId,
                ChapterNumber = entity.ChapterNumber,
                Volume = entity.Volume,
                Language = entity.Language,
                ScanGroup = entity.ScanGroup,
                Quality = entity.Quality,
                AnnouncedUtc = entity.AnnouncedUtc,
                LastSeenUtc = entity.LastSeenUtc,
                Title = entity.Title,
                ExternalMetadataSource = entity.ExternalMetadataSource,
                ExteralMetadataMangaId = entity.ExteralMetadataMangaId
            };
        }

        protected override ManifestEntryEntity MapToEntity(ManifestEntry model)
        {
            return new ManifestEntryEntity
            {
                ManifestHash = model.ManifestHash,
                SeriesId = model.SeriesId,
                ChapterId = model.ChapterId,
                ChapterNumber = model.ChapterNumber,
                Volume = model.Volume,
                Language = model.Language,
                ScanGroup = model.ScanGroup,
                Quality = model.Quality,
                AnnouncedUtc = model.AnnouncedUtc,
                LastSeenUtc = model.LastSeenUtc,
                Title = model.Title,
                ExternalMetadataSource = model.ExternalMetadataSource,
                ExteralMetadataMangaId = model.ExteralMetadataMangaId
            };
        }

        protected override void UpdateEntity(ManifestEntryEntity existing, ManifestEntry model)
        {
            existing.LastSeenUtc = model.LastSeenUtc;
            existing.Title = model.Title;
        }

        public async Task AddAsync(ManifestEntry entry)
        {
            await AddOrUpdateAsync(entry.ManifestHash, entry);
        }

        public async Task DeleteBySeriesIdAsync(string seriesId)
        {
            var entities = await Db.ManifestEntries
                .Where(e => e.SeriesId == seriesId)
                .ToListAsync();

            if (entities.Count > 0)
            {
                Db.ManifestEntries.RemoveRange(entities);
                await Db.SaveChangesAsync();
            }
        }
    }
}
