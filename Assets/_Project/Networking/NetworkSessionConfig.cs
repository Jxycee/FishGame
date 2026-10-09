using System;
using FishGame.Runtime;
using Unity.Netcode;

namespace FishGame.Networking
{
    // Authority: none (immutable configuration). Built once in AppBootstrap (T13) and shared by the local and online sessions.
    // ProtocolVersion is BuildInfo.EffectiveProtocolVersion(dev offset), computed by T13; this type never reads argv.
    public sealed class NetworkSessionConfig
    {
        public NetworkSessionConfig(NetworkManager networkManager, JoinApprovalBridge bridge, ISettings settings,
                                    int protocolVersion, ulong contentHash, bool longTimeout)
        {
            NetworkManager = networkManager ?? throw new ArgumentNullException(nameof(networkManager));
            Bridge = bridge ?? throw new ArgumentNullException(nameof(bridge));
            Settings = settings ?? throw new ArgumentNullException(nameof(settings));
            ProtocolVersion = protocolVersion;
            ContentHash = contentHash;
            LongTimeout = longTimeout;
        }

        public NetworkManager NetworkManager { get; }
        public JoinApprovalBridge Bridge { get; }
        public ISettings Settings { get; }
        public int ProtocolVersion { get; }
        public ulong ContentHash { get; }
        public bool LongTimeout { get; }
    }
}
