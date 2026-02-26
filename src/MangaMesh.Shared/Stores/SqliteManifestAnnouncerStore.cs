using System.Linq.Expressions;
using MangaMesh.Shared.Data;
using MangaMesh.Shared.Data.Entities;
using MangaMesh.Shared.Models; // Ensure ManifestAnnouncer is accessible here
using Microsoft.EntityFrameworkCore;

namespace MangaMesh.Shared.Stores
{
    public class SqliteManifestAnnouncerStore : SqliteStoreBase<ManifestAnnouncer, ManifestAnnouncerEntity, (string ManifestHash, string NodeId)>, IManifestAnnouncerStore
    {
        public SqliteManifestAnnouncerStore(IndexDbContext db) : base(db)
        {
        }

        protected override DbSet<ManifestAnnouncerEntity> GetDbSet() => Db.ManifestAnnouncers;

        protected override (string ManifestHash, string NodeId) GetEntityKey(ManifestAnnouncerEntity entity)
            => (entity.ManifestHash, entity.NodeId);

        protected override Expression<Func<ManifestAnnouncerEntity, bool>> GetKeyPredicate((string ManifestHash, string NodeId) key)
            => a => a.ManifestHash == key.ManifestHash && a.NodeId == key.NodeId;

        protected override ManifestAnnouncer MapToModel(ManifestAnnouncerEntity entity)
        {
            return new ManifestAnnouncer(entity.ManifestHash, entity.NodeId, entity.AnnouncedAt);
        }

        protected override ManifestAnnouncerEntity MapToEntity(ManifestAnnouncer model)
        {
            return new ManifestAnnouncerEntity
            {
                ManifestHash = model.ManifestHash,
                NodeId = model.NodeId,
                AnnouncedAt = model.AnnouncedAt
            };
        }

        protected override void UpdateEntity(ManifestAnnouncerEntity existing, ManifestAnnouncer model)
        {
            existing.AnnouncedAt = model.AnnouncedAt;
        }

        public async Task RecordAsync(string manifestHash, string nodeId, DateTime announcedAt)
        {
            var model = new ManifestAnnouncer(manifestHash, nodeId, announcedAt);
            await AddOrUpdateAsync((manifestHash, nodeId), model);
        }

        public async Task<IEnumerable<ManifestAnnouncer>> GetByManifestHashAsync(string manifestHash)
        {
            return await Db.ManifestAnnouncers
                .AsNoTracking()
                .Where(a => a.ManifestHash == manifestHash)
                .Select(a => new ManifestAnnouncer(a.ManifestHash, a.NodeId, a.AnnouncedAt))
                .ToListAsync();
        }

        public async Task DeleteByManifestHashAsync(string manifestHash)
        {
            var entities = await Db.ManifestAnnouncers
                .Where(a => a.ManifestHash == manifestHash)
                .ToListAsync();

            if (entities.Count > 0)
            {
                Db.ManifestAnnouncers.RemoveRange(entities);
                await Db.SaveChangesAsync();
            }
        }
    }
}
