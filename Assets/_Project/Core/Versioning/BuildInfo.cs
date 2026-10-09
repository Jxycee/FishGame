// Core/Versioning/BuildInfo.cs: wire-protocol and game version constants (spec 4.5).
// ProtocolVersion is bumped by hand on any wire, RPC or networked-prefab change. It goes only into the connection payload;
// NetworkConfig.ProtocolVersion stays 0 (see NetworkConstants.NgoProtocolVersion, T3).
namespace FishGame.Core
{
    public static class BuildInfo
    {
        public const int ProtocolVersion = 1;
        public const string GameVersion = "0.1.0";

        // devProtocolOffset comes from the -dev-protocol-offset flag (acceptance criterion 5); 0 in normal runs.
        public static int EffectiveProtocolVersion(int devProtocolOffset) => ProtocolVersion + devProtocolOffset;
    }
}
