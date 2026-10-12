using System;
using UnityEngine;

namespace FishGame.Data
{
    // Content data only (not networked). Light, sky, toon and fog settings for one day phase (spec 10.3). Owned by T13.
    [Serializable]
    public struct DayPhaseAmbient
    {
        public float startFraction;
        public Color lightColor;
        public float lightIntensity;
        public Color skyTop, skyHorizon, skyGround, toonShade, toonAmbient, fogColor;
        public float fogDensity, warmWeight, coolWeight;
    }
}
