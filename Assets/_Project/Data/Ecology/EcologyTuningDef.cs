using FishGame.Core;
using UnityEngine;

namespace FishGame.Data
{
    // Content data only (not networked). Hotspot timings and radius (spec 12.7, M3-D20); the effects stay formula constants in Core.
    // No tuning initialisers: values come from EcologySeedData. Owned by T9 (M3).
    public sealed class EcologyTuningDef : ScriptableObject
    {
        [SerializeField] private int hotspotFirstMinMin;
        [SerializeField] private int hotspotFirstMinMax;
        [SerializeField] private int hotspotGapMin;
        [SerializeField] private int hotspotGapMax;
        [SerializeField] private int hotspotDurationMin;
        [SerializeField] private int hotspotDurationMax;
        [SerializeField] private float hotspotRadiusM;

        public int HotspotFirstMinMin => hotspotFirstMinMin;
        public int HotspotFirstMinMax => hotspotFirstMinMax;
        public int HotspotGapMin => hotspotGapMin;
        public int HotspotGapMax => hotspotGapMax;
        public int HotspotDurationMin => hotspotDurationMin;
        public int HotspotDurationMax => hotspotDurationMax;
        public float HotspotRadiusM => hotspotRadiusM;

        // Used by the content builder and by tests.
        public void Configure(int hotspotFirstMinMin, int hotspotFirstMinMax, int hotspotGapMin, int hotspotGapMax,
            int hotspotDurationMin, int hotspotDurationMax, float hotspotRadiusM)
        {
            this.hotspotFirstMinMin = hotspotFirstMinMin;
            this.hotspotFirstMinMax = hotspotFirstMinMax;
            this.hotspotGapMin = hotspotGapMin;
            this.hotspotGapMax = hotspotGapMax;
            this.hotspotDurationMin = hotspotDurationMin;
            this.hotspotDurationMax = hotspotDurationMax;
            this.hotspotRadiusM = hotspotRadiusM;
        }

        public EcologyTuning ToCore()
        {
            return new EcologyTuning
            {
                HotspotFirstMinMin = hotspotFirstMinMin,
                HotspotFirstMinMax = hotspotFirstMinMax,
                HotspotGapMin = hotspotGapMin,
                HotspotGapMax = hotspotGapMax,
                HotspotDurationMin = hotspotDurationMin,
                HotspotDurationMax = hotspotDurationMax,
                HotspotRadiusM = hotspotRadiusM,
            };
        }
    }
}
