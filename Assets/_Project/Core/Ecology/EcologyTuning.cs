// Core/Ecology/EcologyTuning.cs: hotspot timings and radius (spec 12.7, M3-D20). Values come from EcologyTuningDef (seeds in
// EcologySeedData); Core holds no defaults. Authority: none (plain data).
namespace FishGame.Core
{
    public struct EcologyTuning
    {
        public int HotspotFirstMinMin, HotspotFirstMinMax, HotspotGapMin, HotspotGapMax, HotspotDurationMin, HotspotDurationMax;   // minutes; seeds 60/120/60/120/45/60
        public float HotspotRadiusM;                 // 3
    }
}
