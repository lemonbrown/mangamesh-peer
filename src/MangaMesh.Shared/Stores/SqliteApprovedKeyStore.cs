using System.Linq.Expressions;
using MangaMesh.Shared.Data;
using MangaMesh.Shared.Data.Entities;
using MangaMesh.Shared.Models;
using Microsoft.EntityFrameworkCore;

namespace MangaMesh.Shared.Stores
{
    public class SqliteApprovedKeyStore : SqliteStoreBase<ApprovedKeyRecord, IndexApprovedKeyEntity, string>, IApprovedKeyStore
    {
        public SqliteApprovedKeyStore(IndexDbContext db) : base(db)
        {
        }

        protected override DbSet<IndexApprovedKeyEntity> GetDbSet() => Db.ApprovedKeys;

        protected override string GetEntityKey(IndexApprovedKeyEntity entity) => entity.PublicKeyBase64;

        protected override Expression<Func<IndexApprovedKeyEntity, bool>> GetKeyPredicate(string key)
            => k => k.PublicKeyBase64 == key;

        protected override ApprovedKeyRecord MapToModel(IndexApprovedKeyEntity entity)
        {
            return new ApprovedKeyRecord
            {
                PublicKeyBase64 = entity.PublicKeyBase64,
                Comment = entity.Comment,
                AddedAt = entity.AddedAt
            };
        }

        protected override IndexApprovedKeyEntity MapToEntity(ApprovedKeyRecord model)
        {
            return new IndexApprovedKeyEntity
            {
                PublicKeyBase64 = model.PublicKeyBase64,
                Comment = model.Comment,
                AddedAt = model.AddedAt.UtcDateTime
            };
        }

        protected override void UpdateEntity(IndexApprovedKeyEntity existing, ApprovedKeyRecord model)
        {
            existing.Comment = model.Comment;
            // Usually don't update AddedAt
        }

        public async Task<bool> IsKeyApprovedAsync(string publicKeyBase64)
        {
            return await Db.ApprovedKeys.AnyAsync(k => k.PublicKeyBase64 == publicKeyBase64);
        }

        public async Task ApproveKeyAsync(string publicKeyBase64, string comment)
        {
            var record = new ApprovedKeyRecord
            {
                PublicKeyBase64 = publicKeyBase64,
                Comment = comment,
                AddedAt = DateTimeOffset.UtcNow
            };
            
            await AddOrUpdateAsync(publicKeyBase64, record);
        }

        public async Task RevokeKeyAsync(string publicKeyBase64)
        {
            await DeleteAsync(publicKeyBase64);
        }

        public async Task<IEnumerable<ApprovedKeyRecord>> GetAllApprovedAsync()
        {
            return await GetAllAsync();
        }
    }
}
