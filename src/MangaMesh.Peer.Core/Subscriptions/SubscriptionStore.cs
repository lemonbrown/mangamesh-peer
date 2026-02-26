using MangaMesh.Peer.Core.Series;
using MangaMesh.Shared.Stores;
using System.Collections.Generic;
using System.IO;
using System.Linq;
using System.Threading;
using System.Threading.Tasks;

namespace MangaMesh.Peer.Core.Subscriptions
{
    public class SubscriptionStore : JsonStoreBase<SeriesSubscription, string>, ISubscriptionStore
    {
        public SubscriptionStore(string rootPath) 
            : base(Path.Combine(rootPath, "subscriptions.json"))
        {
        }

        protected override string GetKey(SeriesSubscription item) => item.SeriesId;

        public async Task<IReadOnlyList<SeriesSubscription>> GetAllAsync(CancellationToken ct = default)
        {
            var all = await base.GetAllAsync();
            return all.ToList().AsReadOnly();
        }

        public async Task AddAsync(SeriesSubscription subscription, CancellationToken ct = default)
        {
            await AddOrUpdateAsync(subscription.SeriesId, subscription);
        }

        public async Task RemoveAsync(SeriesSubscription subscription, CancellationToken ct = default)
        {
            await DeleteAsync(subscription.SeriesId);
        }

        public async Task<bool> ExistsAsync(SeriesSubscription subscription, CancellationToken ct = default)
        {
            var all = await base.GetAllAsync();
            return all.Any(s => s.SeriesId == subscription.SeriesId);
        }
    }
}
