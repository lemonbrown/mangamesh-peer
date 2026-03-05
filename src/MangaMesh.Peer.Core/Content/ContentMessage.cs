using MangaMesh.Peer.Core.Replication;
using System;
using System.Collections.Generic;
using System.Text.Json;
using System.Text.Json.Serialization;

namespace MangaMesh.Peer.Core.Content
{
    [JsonDerivedType(typeof(GetManifest), typeDiscriminator: "GetManifest")]
    [JsonDerivedType(typeof(GetBlob), typeDiscriminator: "GetBlob")]
    [JsonDerivedType(typeof(ManifestData), typeDiscriminator: "ManifestData")]
    [JsonDerivedType(typeof(BlobData), typeDiscriminator: "BlobData")]
    [JsonDerivedType(typeof(BlobDataChunk), typeDiscriminator: "BlobDataChunk")]
    [JsonDerivedType(typeof(ReplicateChunk), typeDiscriminator: "ReplicateChunk")]
    [JsonDerivedType(typeof(ReplicateChunkAck), typeDiscriminator: "ReplicateChunkAck")]
    [JsonDerivedType(typeof(ChapterHealthGossip), typeDiscriminator: "ChapterHealthGossip")]
    [JsonDerivedType(typeof(ChunkReplicaQuery), typeDiscriminator: "ChunkReplicaQuery")]
    [JsonDerivedType(typeof(ChunkReplicaResponse), typeDiscriminator: "ChunkReplicaResponse")]
    public abstract class ContentMessage
    {
        public int SenderPort { get; set; }
        public Guid RequestId { get; set; } = Guid.NewGuid();

        public static ContentMessage? Deserialize(ReadOnlyMemory<byte> payload)
        {
            try
            {
                var json = System.Text.Encoding.UTF8.GetString(payload.Span);
                return JsonSerializer.Deserialize<ContentMessage>(json);
            }
            catch { return null; }
        }

        public byte[] Serialize()
        {
            return JsonSerializer.SerializeToUtf8Bytes(this);
        }
    }

    public class GetManifest : ContentMessage
    {
        public string ContentHash { get; set; } = string.Empty;
    }

    public class GetBlob : ContentMessage
    {
        public string BlobHash { get; set; } = string.Empty;
    }

    public class ManifestData : ContentMessage
    {
        public string ContentHash { get; set; } = string.Empty;
        public byte[] Data { get; set; } = Array.Empty<byte>();
    }

    public class BlobData : ContentMessage
    {
        public string BlobHash { get; set; } = string.Empty;
        public byte[] Data { get; set; } = Array.Empty<byte>();
    }

    /// <summary>
    /// One fragment of a blob that is too large to send in a single DataChannel message.
    /// The client reassembles all chunks (ordered by ChunkIndex) before resolving the fetch.
    /// </summary>
    public class BlobDataChunk : ContentMessage
    {
        public string BlobHash { get; set; } = string.Empty;
        public byte[] Data { get; set; } = Array.Empty<byte>();
        public int ChunkIndex { get; set; }
        public int TotalChunks { get; set; }
    }

    /// <summary>Push request: seeder asks receiver to replicate this chunk.</summary>
    public class ReplicateChunk : ContentMessage
    {
        public string BlobHash { get; set; } = string.Empty;
        public string ChapterId { get; set; } = string.Empty;

        /// <summary>0 = normal, 1 = rare (below min replicas), 2 = urgent repair.</summary>
        public int Priority { get; set; }
    }

    /// <summary>Receiver's response to a ReplicateChunk push request.</summary>
    public class ReplicateChunkAck : ContentMessage
    {
        public string BlobHash { get; set; } = string.Empty;
        public bool Accepted { get; set; }
        /// <summary>Populated when Accepted=false: "StorageFull", "AlreadyOwned", "DiversityLimit".</summary>
        public string? DeclineReason { get; set; }
    }

    /// <summary>Gossip: share chapter health observations with a neighbour.</summary>
    public class ChapterHealthGossip : ContentMessage
    {
        public List<ChapterHealthState> Items { get; set; } = new();
    }

    /// <summary>Ask a peer for estimated replica counts on a set of chunk hashes.</summary>
    public class ChunkReplicaQuery : ContentMessage
    {
        public List<string> BlobHashes { get; set; } = new();
    }

    /// <summary>Response to ChunkReplicaQuery: estimated replica counts per chunk.</summary>
    public class ChunkReplicaResponse : ContentMessage
    {
        public Dictionary<string, int> Estimates { get; set; } = new();
    }
}
