// Core/Versioning/BuildInfo.cs: wire-protocol and game version constants (spec 4.5).
// ProtocolVersion is bumped by hand on any wire, RPC or networked-prefab change. It goes only into the connection payload;
// NetworkConfig.ProtocolVersion stays 0 (see NetworkConstants.NgoProtocolVersion, T3).
// History: 1 = M1; 2 = M2 (T17: FishingRodNetwork RPCs and NetworkVariables, PlayerNetState.NetTool, PlayerCreel);
// 3 = R72 (FlagRight; FlagDive = back pull with a side; no run flags on a spent fish; host fight validation).
namespace FishGame.Core
{
    public static class BuildInfo
    {
        public const int ProtocolVersion = 3;
        public const string GameVersion = "0.1.0";

        // devProtocolOffset comes from the -dev-protocol-offset flag (acceptance criterion 5); 0 in normal runs.
        public static int EffectiveProtocolVersion(int devProtocolOffset) => ProtocolVersion + devProtocolOffset;
    }
}
