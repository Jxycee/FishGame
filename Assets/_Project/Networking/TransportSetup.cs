using System;
using FishGame.Core;
using Unity.Netcode;
using Unity.Netcode.Transports.UTP;

namespace FishGame.Networking
{
    // Authority: none (helper). Configures the plain UnityTransport protocol and timeouts. Never sets Relay data.
    public static class TransportSetup
    {
        public static UnityTransport Get(NetworkManager nm)
        {
            if (nm == null)
            {
                throw new ArgumentNullException(nameof(nm));
            }
            UnityTransport utp = nm.NetworkConfig.NetworkTransport as UnityTransport;
            if (utp == null)
            {
                utp = nm.GetComponent<UnityTransport>();
                if (utp != null)
                {
                    // NGO starts from NetworkConfig.NetworkTransport; a null field would make StartHost fail with a misleading transport error.
                    nm.NetworkConfig.NetworkTransport = utp;
                }
            }
            if (utp == null)
            {
                throw new InvalidOperationException("No UnityTransport found on the NetworkManager.");
            }
            return utp;
        }

        public static void ApplyTimeouts(UnityTransport utp, bool longTimeout)
        {
            utp.ConnectTimeoutMS = NetworkConstants.ConnectTimeoutMS;
            utp.MaxConnectAttempts = NetworkConstants.MaxConnectAttempts;
            utp.DisconnectTimeoutMS = longTimeout ? NetworkConstants.LongDisconnectTimeoutMS : NetworkConstants.DisconnectTimeoutMS;
            utp.HeartbeatTimeoutMS = NetworkConstants.HeartbeatTimeoutMS;
        }

        // Resets Protocol to ProtocolType.UnityTransport (so Relay data never leaks into Solo) and ignores -ip/-port args.
        public static void ConfigureDirect(UnityTransport utp, string address, ushort port, string listenAddress)
        {
            utp.SetConnectionData(true, address, port, listenAddress);
        }
    }
}
