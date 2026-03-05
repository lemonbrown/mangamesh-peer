using MangaMesh.Peer.Core.Transport;

namespace MangaMesh.Peer.Core.Replication;

public interface IPeerScorer
{
    /// <summary>
    /// Ranks candidate peers for replication target selection.
    /// Returns up to <paramref name="count"/> peers, highest score first.
    /// </summary>
    IReadOnlyList<RoutingEntry> RankCandidates(IEnumerable<RoutingEntry> candidates, int count);
}
