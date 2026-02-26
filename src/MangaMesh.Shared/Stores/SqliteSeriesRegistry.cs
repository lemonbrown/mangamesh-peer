using System.Linq.Expressions;
using MangaMesh.Shared.Data;
using MangaMesh.Shared.Data.Entities;
using MangaMesh.Shared.Models;
using Microsoft.EntityFrameworkCore;

namespace MangaMesh.Shared.Stores
{
    public class SqliteSeriesRegistry : SqliteStoreBase<SeriesDefinition, SeriesDefinitionEntity, string>, ISeriesRegistry
    {
        public SqliteSeriesRegistry(IndexDbContext db) : base(db)
        {
        }

        protected override DbSet<SeriesDefinitionEntity> GetDbSet() => Db.SeriesDefinitions;
        protected override string GetEntityKey(SeriesDefinitionEntity entity) => entity.SeriesId;
        protected override Expression<Func<SeriesDefinitionEntity, bool>> GetKeyPredicate(string key) => e => e.SeriesId == key;

        protected override SeriesDefinition MapToModel(SeriesDefinitionEntity entity)
        {
            return new SeriesDefinition
            {
                SeriesId = entity.SeriesId,
                Source = (ExternalMetadataSource)entity.Source,
                ExternalMangaId = entity.ExternalMangaId,
                Title = entity.Title,
                CreatedUtc = entity.CreatedUtc
            };
        }

        protected override SeriesDefinitionEntity MapToEntity(SeriesDefinition model)
        {
            return new SeriesDefinitionEntity
            {
                SeriesId = model.SeriesId,
                Source = (int)model.Source,
                ExternalMangaId = model.ExternalMangaId,
                Title = model.Title,
                CreatedUtc = model.CreatedUtc
            };
        }

        protected override void UpdateEntity(SeriesDefinitionEntity existing, SeriesDefinition model)
        {
            existing.Title = model.Title;
            existing.Source = (int)model.Source;
            existing.ExternalMangaId = model.ExternalMangaId;
        }

        public async Task<SeriesDefinition?> GetByExternalIdAsync(ExternalMetadataSource source, string externalMangaId)
        {
            var sourceInt = (int)source;
            var entity = await GetDbSet()
                .AsNoTracking()
                .FirstOrDefaultAsync(e =>
                    e.Source == sourceInt &&
                    e.ExternalMangaId == externalMangaId);

            return entity == null ? null : MapToModel(entity);
        }

        public Task<SeriesDefinition?> GetByIdAsync(string seriesId) => GetAsync(seriesId);

        public Task RegisterAsync(SeriesDefinition definition) => AddOrUpdateAsync(definition.SeriesId, definition);
    }
}
