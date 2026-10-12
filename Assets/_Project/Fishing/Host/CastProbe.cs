// Authority: none (stateless Physics adapter). The same function runs on the owner (marker, cast request) and on the host (validity of
// the landing the host derives itself), feeding Core CastMath.ProbeReason (spec 9.3). Main thread only; uses one static hit buffer.
using FishGame.Core;
using FishGame.Runtime;
using UnityEngine;

namespace FishGame.Fishing
{
    public static class CastProbe
    {
        private const int HitBufferSize = 16;
        private static readonly RaycastHit[] s_hits = new RaycastHit[HitBufferSize];

        public static int CastMask { get; } = Layers.Mask(Layers.Water) | Layers.Mask(Layers.Ground) | Layers.Mask(Layers.Shore) | Layers.Mask(Layers.Structure);
        public static int BedMask { get; } = Layers.Mask(Layers.Ground);

        // Ray 1 down from (x, FlatHeight + WaterProbeHeight, z) over WaterProbeHeight + BedProbeDistance; the nearest hit (RaycastNonAlloc is
        // unsorted) must be on layer Water. Then ray 2 from that hit down BedProbeDistance against the lakebed gives the depth.
        public static WaterProbe Probe(float x, float z)
        {
            var probe = new WaterProbe();
            var ray = new Ray(new Vector3(x, WaterSurface.FlatHeight + FishingConstants.WaterProbeHeight, z), Vector3.down);
            float length = FishingConstants.WaterProbeHeight + FishingConstants.BedProbeDistance;
            int n = Physics.RaycastNonAlloc(ray, s_hits, length, CastMask, QueryTriggerInteraction.Collide);
            if (n <= 0) return probe;

            int best = 0;
            for (int i = 1; i < n; i++)
            {
                if (s_hits[i].distance < s_hits[best].distance) best = i;
            }

            RaycastHit first = s_hits[best];
            if (first.collider == null || first.collider.gameObject.layer != Layers.Water) return probe;

            probe.FirstHitIsWater = true;
            probe.WaterY = first.point.y;
            if (Physics.Raycast(first.point, Vector3.down, out RaycastHit bed, FishingConstants.BedProbeDistance, BedMask, QueryTriggerInteraction.Ignore))
            {
                probe.HasBed = true;
                probe.Depth = probe.WaterY - bed.point.y;
            }
            return probe;
        }
    }
}
