namespace FishGame.Core
{
    public static class NetworkConstants
    {
        public const ushort DefaultPort = 7777;          // solo first try; dev-direct host + clients
        public const ushort SoloPortRetryCount = 10;     // solo tries Port .. Port+9 (7777..7786 by default)
        public const ushort TestPort = 7790;             // PlayMode tests set LocalNetworkSession.Port = TestPort
        public const string LoopbackAddress = "127.0.0.1";
        public const uint TickRate = 30;
        public const int MaxPlayers = 4;
        public const ushort NgoProtocolVersion = 0;      // NetworkConfig.ProtocolVersion, never changes
        public const int ConnectTimeoutMS = 1000;
        public const int MaxConnectAttempts = 10;
        public const int DisconnectTimeoutMS = 10000;
        public const int LongDisconnectTimeoutMS = 120000; // -dev-long-timeout
        public const int HeartbeatTimeoutMS = 500;
        public const double AsyncTimeoutSeconds = 15.0;
        public const double VersionMismatchWindowSeconds = 8.0; // empty-reason drop sooner than this while Connecting => VersionMismatch
    }
}
