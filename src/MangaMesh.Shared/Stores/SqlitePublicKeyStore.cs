using System.Linq.Expressions;
using MangaMesh.Shared.Data;
using MangaMesh.Shared.Data.Entities;
using MangaMesh.Shared.Models;
using Microsoft.EntityFrameworkCore;

namespace MangaMesh.Shared.Stores
{
    public class SqlitePublicKeyStore : SqliteStoreBase<PublicKeyRecord, IndexKeyEntity, string>, IPublicKeyStore
    {
        public SqlitePublicKeyStore(IndexDbContext context) : base(context)
        {
        }

        protected override DbSet<IndexKeyEntity> GetDbSet() => Db.Keys;

        protected override string GetEntityKey(IndexKeyEntity entity) => entity.PublicKeyBase64;

        protected override Expression<Func<IndexKeyEntity, bool>> GetKeyPredicate(string key)
            => k => k.PublicKeyBase64 == key;

        protected override PublicKeyRecord MapToModel(IndexKeyEntity entity)
        {
            return new PublicKeyRecord
            {
                PublicKeyBase64 = entity.PublicKeyBase64,
                RegisteredAt = entity.RegisteredAt,
                Revoked = entity.Revoked
            };
        }

        protected override IndexKeyEntity MapToEntity(PublicKeyRecord model)
        {
            return new IndexKeyEntity
            {
                PublicKeyBase64 = model.PublicKeyBase64,
                RegisteredAt = model.RegisteredAt,
                Revoked = model.Revoked
            };
        }

        protected override void UpdateEntity(IndexKeyEntity existing, PublicKeyRecord model)
        {
            existing.Revoked = model.Revoked;
        }

        public async Task StoreAsync(PublicKeyRecord record)
        {
            var exists = await Db.Keys.AnyAsync(k => k.PublicKeyBase64 == record.PublicKeyBase64);
            if (!exists)
            {
                Db.Keys.Add(MapToEntity(record));
                await Db.SaveChangesAsync();
            }
        }

        public async Task<PublicKeyRecord?> GetByKeyAsync(string publicKeyBase64)
        {
            var decoded = Uri.UnescapeDataString(publicKeyBase64);
            return await GetAsync(decoded);
        }

        public async Task RevokeAsync(string publicKeyId)
        {
            var entity = await Db.Keys.FindAsync(publicKeyId);
            if (entity != null)
            {
                entity.Revoked = true;
                await Db.SaveChangesAsync();
            }
        }

        public Task<PublicKeyRecord?> GetByUserIdAsync(string userId) => Task.FromResult<PublicKeyRecord?>(null);
        public Task<PublicKeyRecord?> GetByKeyIdAsync(string publicKeyId) => Task.FromResult<PublicKeyRecord?>(null);
    }
}
