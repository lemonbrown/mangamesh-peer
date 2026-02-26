using System.Linq.Expressions;
using MangaMesh.Shared.Data;
using MangaMesh.Shared.Data.Entities;
using MangaMesh.Shared.Models;
using Microsoft.EntityFrameworkCore;

namespace MangaMesh.Shared.Stores
{
    public class SqliteChallengeStore : SqliteStoreBase<KeyChallenge, IndexChallengeEntity, string>, IChallengeStore
    {
        public SqliteChallengeStore(IndexDbContext context) : base(context)
        {
        }

        protected override DbSet<IndexChallengeEntity> GetDbSet() => Db.Challenges;

        protected override string GetEntityKey(IndexChallengeEntity entity) => entity.Id;

        protected override Expression<Func<IndexChallengeEntity, bool>> GetKeyPredicate(string key)
            => c => c.Id == key;

        protected override KeyChallenge MapToModel(IndexChallengeEntity entity)
        {
            return new KeyChallenge
            {
                Id = entity.Id,
                UserId = entity.UserId,
                Nonce = entity.Nonce,
                ExpiresAt = entity.ExpiresAt
            };
        }

        protected override IndexChallengeEntity MapToEntity(KeyChallenge model)
        {
            return new IndexChallengeEntity
            {
                Id = model.Id,
                UserId = model.UserId,
                Nonce = model.Nonce,
                ExpiresAt = model.ExpiresAt
            };
        }

        protected override void UpdateEntity(IndexChallengeEntity existing, KeyChallenge model)
        {
            existing.ExpiresAt = model.ExpiresAt; // Or whatever update rules apply
        }

        public async Task StoreAsync(KeyChallenge challenge)
        {
            Db.Challenges.Add(MapToEntity(challenge));
            await Db.SaveChangesAsync();
        }

        public async Task CleanupExpiredAsync()
        {
            var now = DateTime.UtcNow;
            var expired = await Db.Challenges.Where(c => c.ExpiresAt <= now).ToListAsync();

            if (expired.Any())
            {
                Db.Challenges.RemoveRange(expired);
                await Db.SaveChangesAsync();
            }
        }
    }
}
