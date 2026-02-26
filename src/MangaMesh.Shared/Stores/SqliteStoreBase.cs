using System.Linq.Expressions;
using MangaMesh.Shared.Data;
using Microsoft.EntityFrameworkCore;

namespace MangaMesh.Shared.Stores
{
    public abstract class SqliteStoreBase<TModel, TEntity, TKey>
        where TModel : class
        where TEntity : class
        where TKey : notnull
    {
        protected readonly IndexDbContext Db;

        protected SqliteStoreBase(IndexDbContext db)
        {
            Db = db;
        }

        protected abstract DbSet<TEntity> GetDbSet();
        protected abstract TKey GetEntityKey(TEntity entity);
        protected abstract Expression<Func<TEntity, bool>> GetKeyPredicate(TKey key);
        protected abstract TModel MapToModel(TEntity entity);
        protected abstract TEntity MapToEntity(TModel model);
        protected abstract void UpdateEntity(TEntity existing, TModel model);

        public virtual async Task<TModel?> GetAsync(TKey key)
        {
            var entity = await GetDbSet()
                .AsNoTracking()
                .FirstOrDefaultAsync(GetKeyPredicate(key));

            return entity != null ? MapToModel(entity) : null;
        }

        public virtual async Task<IEnumerable<TModel>> GetAllAsync()
        {
            var entities = await GetDbSet()
                .AsNoTracking()
                .ToListAsync();

            return entities.Select(MapToModel);
        }

        protected async Task AddOrUpdateAsync(TKey key, TModel model)
        {
            var existing = await GetDbSet().FirstOrDefaultAsync(GetKeyPredicate(key));
            if (existing != null)
            {
                UpdateEntity(existing, model);
            }
            else
            {
                GetDbSet().Add(MapToEntity(model));
            }

            await Db.SaveChangesAsync();
        }

        public virtual async Task DeleteAsync(TKey key)
        {
            var entity = await GetDbSet().FindAsync(key);
            if (entity != null)
            {
                GetDbSet().Remove(entity);
                await Db.SaveChangesAsync();
            }
        }
    }
}
