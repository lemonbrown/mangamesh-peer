using MangaMesh.Peer.Core.Keys;
using Microsoft.Extensions.DependencyInjection;

namespace MangaMesh.Peer.ClientApi.Services
{
    public class SingletonKeyStore : IKeyStore
    {
        private readonly IServiceScopeFactory _scopeFactory;

        public SingletonKeyStore(IServiceScopeFactory scopeFactory)
        {
            _scopeFactory = scopeFactory;
        }

        public async Task<PublicPrivateKeyPair?> GetAsync()
        {
            using (var scope = _scopeFactory.CreateScope())
            {
                var store = scope.ServiceProvider.GetRequiredKeyedService<IKeyStore>("ScopedKeyStore");
                return await store.GetAsync();
            }
        }

        public async Task SaveAsync(string publicKeyBase64, string privateKeyBase64)
        {
            using (var scope = _scopeFactory.CreateScope())
            {
                var store = scope.ServiceProvider.GetRequiredKeyedService<IKeyStore>("ScopedKeyStore");
                await store.SaveAsync(publicKeyBase64, privateKeyBase64);
            }
        }
    }
}
