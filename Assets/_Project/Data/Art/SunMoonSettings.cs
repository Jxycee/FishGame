using System;
using UnityEngine;

namespace FishGame.Data
{
    // Content data only (not networked). The sun arc and the moon light (spec 10.3): the sun arcs east to west between sunriseFraction and
    // sunsetFraction of the day (elevation >= minElevationDeg while up); within flipWindow of sunset the single directional light fades out,
    // flips to the fixed moon direction (moonEulerDeg) with moonTint at moonIntensity and fades back in. blendWindow is the phase
    // cross-fade length (M3-D24). Public camelCase fields (M3-D76). Owned by T9 (M3); read by T18.
    [Serializable]
    public struct SunMoonSettings
    {
        public float sunriseFraction, sunsetFraction, minElevationDeg, flipWindow, moonIntensity;
        public Vector3 moonEulerDeg;
        public Color moonTint;
        public float blendWindow;
    }
}
