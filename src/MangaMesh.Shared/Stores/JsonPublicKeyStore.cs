using MangaMesh.Shared.Models;

namespace MangaMesh.Shared.Stores
{
    public class JsonKeyStore : JsonStoreBase<PublicKeyRecord, string>, IPublicKeyStore
    {
        public JsonKeyStore(string filePath, string filename) 
            : base(Path.Combine(filePath, filename))
        {
            Directory.CreateDirectory(filePath);
            if (!File.Exists(FilePath))
            {
                File.Create(FilePath).Dispose();
            }
        }

        protected override string GetKey(PublicKeyRecord item) => item.PublicKeyBase64;

        public async Task StoreAsync(PublicKeyRecord record)
        {
            await AddOrUpdateAsync(record.PublicKeyBase64, record);
        }

        public async Task<PublicKeyRecord?> GetByKeyAsync(string publicKeyBase64)
        {
            var decoded = Uri.UnescapeDataString(publicKeyBase64);
            var match = await GetAsync(decoded);
            
            // Re-decode for backwards compatibility parity
            if (match != null)
            {
                match.PublicKeyBase64 = Uri.UnescapeDataString(match.PublicKeyBase64);
            }
            return match;
        }

        public Task<PublicKeyRecord?> GetByUserIdAsync(string userId)
        {
            throw new NotImplementedException();
        }

        public Task<PublicKeyRecord?> GetByKeyIdAsync(string publicKeyId)
        {
            throw new NotImplementedException();
        }

        public Task RevokeAsync(string publicKeyId)
        {
            throw new NotImplementedException();
        }
    }
}
