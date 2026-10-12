using System;
using FishGame.Core;
using UnityEngine;

namespace FishGame.Runtime
{
    // Authority: none. Pure shared water height for every peer (spec 10.4).
    // Waves come from WaterTimeDriver (T22) via Configure; the clock via SetTimeSource (ServerTime in sessions).
    // Gameplay never reads _WaterTime.
    public static class WaterSurface
    {
        public const float FlatHeight = 0f;

        private static WaveSet s_waves = WaveSet.Flat;
        private static Func<double> s_now;

        public static void Configure(in WaveSet waves)
        {
            s_waves = waves;
        }

        // null restores the default clock (Time.timeSinceLevelLoad).
        public static void SetTimeSource(Func<double> now)
        {
            s_now = now;
        }

        public static double Now
        {
            get { return s_now != null ? s_now() : Time.timeSinceLevelLoad; }
        }

        public static float HeightAt(float x, float z)
        {
            // A flat set never reads the clock.
            if (WaveMath.AmplitudeSum(s_waves) == 0f) return FlatHeight;
            return HeightAt(x, z, Now);
        }

        public static float HeightAt(float x, float z, double t)
        {
            return FlatHeight + WaveMath.HeightAt(s_waves, x, z, t);
        }

        public static void ResetToFlat()
        {
            s_waves = WaveSet.Flat;
            s_now = null;
        }

        // Enter Play Mode options are on in this project, so statics survive play sessions.
        [RuntimeInitializeOnLoadMethod(RuntimeInitializeLoadType.SubsystemRegistration)]
        private static void ResetStatics()
        {
            ResetToFlat();
        }
    }
}
