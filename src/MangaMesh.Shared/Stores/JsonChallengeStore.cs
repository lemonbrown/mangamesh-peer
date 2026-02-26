using MangaMesh.Shared.Models;

namespace MangaMesh.Shared.Stores
{
    public class JsonChallengeStore : JsonStoreBase<KeyChallenge, string>, IChallengeStore
    {
        public JsonChallengeStore(string filePath, string filename) 
            : base(Path.Combine(filePath, filename))
        {
            Directory.CreateDirectory(filePath);
            if (!File.Exists(FilePath))
            {
                File.Create(FilePath).Dispose();
            }
        }

        protected override string GetKey(KeyChallenge item) => item.Id;

        public async Task StoreAsync(KeyChallenge challenge)
        {
            await AddOrUpdateAsync(challenge.Id, challenge);
        }

        public async Task CleanupExpiredAsync()
        {
            var all = (await GetAllAsync()).ToList();
            var now = DateTime.UtcNow;
            var removed = all.RemoveAll(c => c.ExpiresAt <= now) > 0;

            if (removed)
                await JsonFileStore.SaveAsync(FilePath, all);
        }
    }
}
