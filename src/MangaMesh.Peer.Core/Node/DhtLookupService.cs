using System.Text;
using System.Text.Json;
using MangaMesh.Peer.Core.Helpers;
using MangaMesh.Peer.Core.Transport;
using Microsoft.Extensions.Logging;

namespace MangaMesh.Peer.Core.Node
{
    public class DhtLookupService : IDhtLookupService
    {
        private readonly INodeIdentity _identity;
        private readonly IRoutingTable _routingTable;
        private readonly IDhtStorage _storage;
        private readonly Func<NodeAddress, DhtMessage, bool, Task<DhtMessage?>> _sendMessage;
        private readonly Func<int> _getTransportPort;
        private readonly Func<List<RoutingEntry>> _getBootstrapNodes;
        private readonly ILogger _logger;

        private const int MaxQueries = 20;
        private const int KClosest = 20;

        public DhtLookupService(
            INodeIdentity identity,
            IRoutingTable routingTable,
            IDhtStorage storage,
            Func<NodeAddress, DhtMessage, bool, Task<DhtMessage?>> sendMessage,
            Func<int> getTransportPort,
            Func<List<RoutingEntry>> getBootstrapNodes,
            ILogger logger)
        {
            _identity = identity;
            _routingTable = routingTable;
            _storage = storage;
            _sendMessage = sendMessage;
            _getTransportPort = getTransportPort;
            _getBootstrapNodes = getBootstrapNodes;
            _logger = logger;
        }

        public async Task<List<byte[]>> FindValueAsync(byte[] contentHash)
        {
            var resultNodes = new List<byte[]>();
            var visited = new HashSet<string>();
            var nodesToQuery = InitializeCandidates(contentHash);

            int queriedCount = 0;

            while (nodesToQuery.Count > 0 && queriedCount < MaxQueries)
            {
                var candidate = nodesToQuery.FirstOrDefault(n => !visited.Contains($"{n.Address.Host}:{n.Address.Port}"));
                if (candidate == null) break;

                visited.Add($"{candidate.Address.Host}:{candidate.Address.Port}");
                nodesToQuery.Remove(candidate);
                queriedCount++;

                var message = CreateFindValueMessage(contentHash);
                var response = await _sendMessage(candidate.Address, message, true);

                if (response != null)
                {
                    UpdateNodeIdentity(candidate, response);

                    if (response.Type == DhtMessageType.Value)
                    {
                        var providerEntries = JsonSerializer.Deserialize<List<NodeEntry>>(
                            Encoding.UTF8.GetString(response.Payload),
                            new JsonSerializerOptions { PropertyNameCaseInsensitive = true });
                        if (providerEntries != null)
                        {
                            foreach (var e in providerEntries)
                            {
                                if (!string.IsNullOrEmpty(e.NodeId))
                                    resultNodes.Add(Convert.FromHexString(e.NodeId));
                            }
                        }

                        if (resultNodes.Count > 0) break;
                    }
                    else if (response.Type == DhtMessageType.Nodes)
                    {
                        AddDiscoveredNodes(response, visited, nodesToQuery, contentHash);
                    }
                }
            }

            resultNodes.AddRange(_storage.GetNodesForContent(contentHash));
            return resultNodes.Distinct(new ByteArrayComparer()).ToList();
        }

        public async Task<List<DhtNode.ProviderInfo>> FindValueWithAddressAsync(byte[] contentHash)
        {
            var providers = new List<DhtNode.ProviderInfo>();
            var visited = new HashSet<string>();
            var nodesToQuery = InitializeCandidates(contentHash);

            int queriedCount = 0;

            while (nodesToQuery.Count > 0 && queriedCount < MaxQueries)
            {
                var candidate = nodesToQuery.FirstOrDefault(n => !visited.Contains($"{n.Address.Host}:{n.Address.Port}"));
                if (candidate == null) break;

                visited.Add($"{candidate.Address.Host}:{candidate.Address.Port}");
                nodesToQuery.Remove(candidate);
                queriedCount++;

                var message = CreateFindValueMessage(contentHash);
                var response = await _sendMessage(candidate.Address, message, true);

                if (response != null)
                {
                    UpdateNodeIdentity(candidate, response);

                    if (response.Type == DhtMessageType.Value)
                    {
                        var providerEntries = JsonSerializer.Deserialize<List<NodeEntry>>(
                            Encoding.UTF8.GetString(response.Payload),
                            new JsonSerializerOptions { PropertyNameCaseInsensitive = true });
                        if (providerEntries != null)
                        {
                            foreach (var entry in providerEntries)
                            {
                                if (string.IsNullOrEmpty(entry.NodeId)) continue;
                                var pid = Convert.FromHexString(entry.NodeId);

                                if (!string.IsNullOrEmpty(entry.Host) && entry.Port > 0)
                                {
                                    // Address came with the response — use it directly.
                                    providers.Add(new DhtNode.ProviderInfo
                                    {
                                        NodeId = pid,
                                        Address = new NodeAddress(entry.Host, entry.Port, HttpApiPort: entry.HttpApiPort, WebRtcEnabled: entry.WebRtcEnabled)
                                    });
                                }
                                else
                                {
                                    // Address unknown to the responder (e.g. the node itself).
                                    // Fall back to routing table or sender envelope.
                                    var address = _routingTable.GetAddressForNode(pid);
                                    if (address != null)
                                    {
                                        providers.Add(new DhtNode.ProviderInfo { NodeId = pid, Address = address });
                                    }
                                    else if (pid.SequenceEqual(response.SenderNodeId) && !string.IsNullOrEmpty(response.ComputedSenderIp))
                                    {
                                        providers.Add(new DhtNode.ProviderInfo
                                        {
                                            NodeId = pid,
                                            Address = new NodeAddress(response.ComputedSenderIp, response.SenderPort, HttpApiPort: response.SenderHttpApiPort, WebRtcEnabled: response.SupportsWebRtc)
                                        });
                                    }
                                }
                            }
                        }

                        if (providers.Count > 0) return providers;
                    }
                    else if (response.Type == DhtMessageType.Nodes)
                    {
                        AddDiscoveredNodes(response, visited, nodesToQuery, contentHash);
                    }
                }
            }

            return providers;
        }

        public async Task<List<RoutingEntry>> FindNodeAsync(byte[] nodeId, RoutingEntry? bootstrap = null)
        {
            var closestNodes = new List<RoutingEntry>();
            var queriedNodes = new HashSet<string>();

            if (bootstrap != null)
                closestNodes.Add(bootstrap);
            else
                closestNodes.AddRange(_routingTable.FindClosest(nodeId, KClosest));

            for (int i = 0; i < closestNodes.Count; i++)
            {
                var node = closestNodes[i];
                var nodeKey = $"{node.Address.Host}:{node.Address.Port}";

                if (queriedNodes.Contains(nodeKey)) continue;
                queriedNodes.Add(nodeKey);

                _logger.LogDebug("Looking for node [{NodeId}] - [{Host}:{Port}]",
                    Convert.ToHexString(node.NodeId ?? Array.Empty<byte>()),
                    node.Address.Host, node.Address.Port);

                var message = new DhtMessage
                {
                    Type = DhtMessageType.FindNode,
                    SenderNodeId = _identity.NodeId,
                    Payload = nodeId,
                    TimestampUtc = DateTime.UtcNow,
                    Signature = _identity.Sign(nodeId),
                    SenderPort = _getTransportPort()
                };

                var response = await _sendMessage(node.Address, message, true);

                if (response != null)
                {
                    _logger.LogDebug("Received response from {IP}:{Port}. SenderID len: {Len}",
                        response.ComputedSenderIp, response.SenderPort, response.SenderNodeId?.Length ?? 0);

                    if ((node.NodeId == null || node.NodeId.Length == 0) && response.SenderNodeId?.Length > 0)
                    {
                        _logger.LogDebug("Updating NodeID for bootstrap node to {NodeId}",
                            Convert.ToHexString(response.SenderNodeId));
                        node.NodeId = response.SenderNodeId;
                        _routingTable.AddOrUpdate(new RoutingEntry
                        {
                            NodeId = node.NodeId,
                            Address = node.Address,
                            LastSeenUtc = DateTime.UtcNow
                        });
                    }

                    if (response.Type == DhtMessageType.Nodes)
                    {
                        try
                        {
                            var nodesJson = Encoding.UTF8.GetString(response.Payload);
                            var discovered = JsonSerializer.Deserialize<List<NodeEntry>>(nodesJson,
                                new JsonSerializerOptions { PropertyNameCaseInsensitive = true });

                            if (discovered != null)
                            {
                                foreach (var d in discovered)
                                {
                                    if (!string.IsNullOrEmpty(d.NodeId) && !string.IsNullOrEmpty(d.Host) && d.Port > 0)
                                    {
                                        var discoveredId = Convert.FromHexString(d.NodeId);
                                        var entry = new RoutingEntry
                                        {
                                            NodeId = discoveredId,
                                            Address = new NodeAddress(d.Host, d.Port, HttpApiPort: d.HttpApiPort, WebRtcEnabled: d.WebRtcEnabled),
                                            LastSeenUtc = DateTime.UtcNow
                                        };
                                        _routingTable.AddOrUpdate(entry);

                                        if (!closestNodes.Any(n => n.NodeId != null && n.NodeId.SequenceEqual(discoveredId)))
                                            closestNodes.Add(entry);
                                    }
                                }
                            }
                        }
                        catch (Exception ex)
                        {
                            _logger.LogWarning(ex, "Failed to parse FindNode response");
                        }
                    }
                }
                else
                {
                    _logger.LogDebug("Timeout or no response from {Host}:{Port}", node.Address.Host, node.Address.Port);
                }
            }
            return closestNodes;
        }

        private List<RoutingEntry> InitializeCandidates(byte[] contentHash)
        {
            var nodesToQuery = new List<RoutingEntry>();
            nodesToQuery.AddRange(_routingTable.FindClosest(contentHash, KClosest));

            if (nodesToQuery.Count == 0)
                nodesToQuery.AddRange(_getBootstrapNodes());

            SortByDistance(nodesToQuery, contentHash);
            return nodesToQuery;
        }

        private DhtMessage CreateFindValueMessage(byte[] contentHash)
        {
            return new DhtMessage
            {
                Type = DhtMessageType.FindValue,
                SenderNodeId = _identity.NodeId,
                Payload = contentHash,
                TimestampUtc = DateTime.UtcNow,
                Signature = _identity.Sign(contentHash),
                SenderPort = _getTransportPort()
            };
        }

        private void UpdateNodeIdentity(RoutingEntry candidate, DhtMessage response)
        {
            if ((candidate.NodeId == null || candidate.NodeId.Length == 0) && response.SenderNodeId?.Length > 0)
            {
                candidate.NodeId = response.SenderNodeId;
                _routingTable.AddOrUpdate(new RoutingEntry
                {
                    NodeId = response.SenderNodeId,
                    Address = candidate.Address,
                    LastSeenUtc = DateTime.UtcNow
                });
            }
        }

        private void AddDiscoveredNodes(DhtMessage response, HashSet<string> visited, List<RoutingEntry> nodesToQuery, byte[] contentHash)
        {
            try
            {
                var nodesJson = Encoding.UTF8.GetString(response.Payload);
                var discovered = JsonSerializer.Deserialize<List<NodeEntry>>(nodesJson,
                    new JsonSerializerOptions { PropertyNameCaseInsensitive = true });

                if (discovered != null)
                {
                    foreach (var d in discovered)
                    {
                        if (!string.IsNullOrEmpty(d.NodeId) && d.Port > 0)
                        {
                            var dId = Convert.FromHexString(d.NodeId);
                            var newEntry = new RoutingEntry
                            {
                                NodeId = dId,
                                Address = new NodeAddress(d.Host, d.Port, HttpApiPort: d.HttpApiPort),
                                LastSeenUtc = DateTime.UtcNow
                            };
                            _routingTable.AddOrUpdate(newEntry);
                            if (!visited.Contains($"{d.Host}:{d.Port}"))
                                nodesToQuery.Add(newEntry);
                        }
                    }
                    SortByDistance(nodesToQuery, contentHash);
                }
            }
            catch
            {
                // ignore parse errors
            }
        }

        private static void SortByDistance(List<RoutingEntry> nodes, byte[] target)
        {
            nodes.Sort((a, b) =>
            {
                if (a.NodeId == null || a.NodeId.Length == 0) return 1;
                if (b.NodeId == null || b.NodeId.Length == 0) return -1;
                return Crypto.XorDistance(a.NodeId, target).CompareTo(Crypto.XorDistance(b.NodeId, target));
            });
        }

        private sealed class ByteArrayComparer : IEqualityComparer<byte[]>
        {
            public bool Equals(byte[]? x, byte[]? y)
            {
                if (x == null || y == null) return x == y;
                return x.SequenceEqual(y);
            }

            public int GetHashCode(byte[] obj) => BitConverter.ToInt32(obj, 0);
        }
    }
}
