// Core/Fishing/RodStats.cs: rod numbers as Core sees them; mirrors RodDef (C 7.5). No default tuning (C 5.1).
namespace FishGame.Core
{
    public struct RodStats
    {
        public byte Tier;
        public float CastMaxDist, ReelSpeed, LineStrength, SteerRate, RelaxMult;
        public float AngleWindow;   // parked by R72 (unused)

        // Farthest the line can pay out before it is spooled: the rod's cast range plus the tuning spool extra.
        public float MaxLineOut(float spoolExtra) => CastMaxDist + spoolExtra;
    }
}
