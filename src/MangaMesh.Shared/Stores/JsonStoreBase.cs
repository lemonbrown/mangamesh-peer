namespace MangaMesh.Shared.Stores
{
    public abstract class JsonStoreBase<TModel, TKey>
        where TModel : class
        where TKey : notnull
    {
        protected readonly string FilePath;

        protected JsonStoreBase(string filePath)
        {
            FilePath = filePath;
        }

        protected abstract TKey GetKey(TModel item);

        public virtual async Task<TModel?> GetAsync(TKey key)
        {
            var all = await GetAllAsync();
            return all.FirstOrDefault(x => EqualityComparer<TKey>.Default.Equals(GetKey(x), key));
        }

        public virtual async Task<IEnumerable<TModel>> GetAllAsync()
        {
            return await JsonFileStore.LoadAsync<TModel>(FilePath);
        }

        protected async Task AddOrUpdateAsync(TKey key, TModel model)
        {
            var all = (await GetAllAsync()).ToList();
            var existingIndex = all.FindIndex(x => EqualityComparer<TKey>.Default.Equals(GetKey(x), key));

            if (existingIndex >= 0)
            {
                all[existingIndex] = model;
            }
            else
            {
                all.Add(model);
            }

            await JsonFileStore.SaveAsync(FilePath, all);
        }

        public virtual async Task DeleteAsync(TKey key)
        {
            var all = (await GetAllAsync()).ToList();
            var count = all.RemoveAll(x => EqualityComparer<TKey>.Default.Equals(GetKey(x), key));
            
            if (count > 0)
            {
                await JsonFileStore.SaveAsync(FilePath, all);
            }
        }
    }
}
