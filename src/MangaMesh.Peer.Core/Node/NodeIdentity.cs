using MangaMesh.Peer.Core.Helpers;
using MangaMesh.Peer.Core.Keys;
using Microsoft.Extensions.Logging;
using NSec.Cryptography;
using System;

namespace MangaMesh.Peer.Core.Node
{
    public class NodeIdentity : INodeIdentity
    {
        public byte[] NodeId { get; private set; }
        public byte[] PublicKey { get; private set; }
        private byte[] _privateKey;

        public NodeIdentity(
            IKeyPairService keyPairService,
            Microsoft.Extensions.Configuration.IConfiguration configuration,
            IKeyStore keyStore,
            ILogger<NodeIdentity> logger)
        {
            string? pubKeyBase64 = configuration["Node:PublicKey"];
            string? privKeyBase64 = configuration["Node:PrivateKey"];

            if (!string.IsNullOrEmpty(pubKeyBase64) && !string.IsNullOrEmpty(privKeyBase64))
            {
                logger.LogInformation("NodeIdentity loaded from configuration (key prefix: {Prefix}...)",
                    pubKeyBase64[..Math.Min(10, pubKeyBase64.Length)]);
                PublicKey = Convert.FromBase64String(pubKeyBase64);
                _privateKey = Convert.FromBase64String(privKeyBase64);
            }
            else
            {
                var storedKeys = keyStore.GetAsync().Result;
                if (storedKeys != null)
                {
                    logger.LogInformation("NodeIdentity loaded from key store (key prefix: {Prefix}...)",
                        storedKeys.PublicKeyBase64[..Math.Min(10, storedKeys.PublicKeyBase64.Length)]);
                    PublicKey = Convert.FromBase64String(storedKeys.PublicKeyBase64);
                    _privateKey = Convert.FromBase64String(storedKeys.PrivateKeyBase64);
                }
                else
                {
                    logger.LogInformation("No existing identity found — generating new Ed25519 key pair");
                    var result = keyPairService.GenerateKeyPairBase64Async().Result;
                    PublicKey = Convert.FromBase64String(result.PublicKeyBase64);
                    _privateKey = Convert.FromBase64String(result.PrivateKeyBase64);
                }
            }

            NodeId = Crypto.Sha256(PublicKey);
            logger.LogInformation("Active NodeID: {NodeId}", Convert.ToHexString(NodeId).ToLower());
        }

        public byte[] Sign(byte[] data)
        {
            return Crypto.Ed25519Sign(_privateKey, data);
        }

        public bool Verify(byte[] data, byte[] signature)
        {
            return Crypto.Ed25519Verify(PublicKey, data, signature);
        }
    }
}
