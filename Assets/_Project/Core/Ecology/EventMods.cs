// Core/Ecology/EventMods.cs: daily-event and hazard modifiers on the resolver (M4 modifiers, M5 hazards; neutral in M3).
// A default struct is NOT valid (every field 0 would silently zero every weight, M3-D46): callers use EventMods.Neutral.
// Authority: none (plain data; the host fills it).
namespace FishGame.Core
{
    public struct EventMods
    {
        public float JunkMult, RareChanceMult, BiteTimeMult;
        public float NocturnalMult, PredatorMult, BottomMult, SchoolingMult, SurfaceMult;

        public static readonly EventMods Neutral = new EventMods
        {
            JunkMult = 1f, RareChanceMult = 1f, BiteTimeMult = 1f,
            NocturnalMult = 1f, PredatorMult = 1f, BottomMult = 1f, SchoolingMult = 1f, SurfaceMult = 1f,
        };

        // Product of the multipliers of the set tag bits; None -> 1.
        public float WeightFor(FishTags tags)
        {
            float w = 1f;
            if ((tags & FishTags.Nocturnal) != 0) w *= NocturnalMult;
            if ((tags & FishTags.Predator) != 0) w *= PredatorMult;
            if ((tags & FishTags.Bottom) != 0) w *= BottomMult;
            if ((tags & FishTags.Schooling) != 0) w *= SchoolingMult;
            if ((tags & FishTags.Surface) != 0) w *= SurfaceMult;
            return w;
        }

        // Every field finite and > 0.
        public bool IsValid =>
            Ok(JunkMult) && Ok(RareChanceMult) && Ok(BiteTimeMult) && Ok(NocturnalMult) && Ok(PredatorMult)
            && Ok(BottomMult) && Ok(SchoolingMult) && Ok(SurfaceMult);

        private static bool Ok(float v) => v > 0f && !float.IsInfinity(v);
    }
}
