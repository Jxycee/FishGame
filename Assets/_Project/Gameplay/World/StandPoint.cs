// Gameplay/World/StandPoint.cs - Authority: none (static marker read by the editor bake; no collider; layer Default).
// A place a player can stand and cast from (spec 12.5, C 7.16, M3-D6, M3-D47). The shore-grid bake reads every StandPoint in the scene
// for the reachability ring; the facing is informational in M3 (reach is any facing).
using UnityEngine;

namespace FishGame.Gameplay
{
    public sealed class StandPoint : MonoBehaviour
    {
        [SerializeField] private float facingDeg;   // yaw in degrees, 0 = +z, 90 = +x (Unity yaw)

        public float FacingDeg => facingDeg;

        // Stores the value normalised to [0, 360); a non-finite value stores 0.
        public void Configure(float facingDeg)
        {
            if (float.IsNaN(facingDeg) || float.IsInfinity(facingDeg)) { this.facingDeg = 0f; return; }
            float f = facingDeg % 360f;
            if (f < 0f) f += 360f;
            if (f >= 360f) f = 0f;
            this.facingDeg = f;
        }
    }
}
