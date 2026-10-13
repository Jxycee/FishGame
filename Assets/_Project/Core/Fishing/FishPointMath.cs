// Core/Fishing/FishPointMath.cs: where the fish is during a fight (spec 9.1 glossary, C 7.5).
// Angle off the cast line = lateral * lateralMaxDeg; positive lateral is right of the cast line.
using System;

namespace FishGame.Core
{
    public static class FishPointMath
    {
        public static void FishPoint(float tipX, float tipZ, float castYawDeg, float lateral, float lateralMaxDeg,
                                     float lineLength, out float x, out float z)
        {
            double angle = ((double)castYawDeg + (double)lateral * lateralMaxDeg) * (Math.PI / 180.0);
            x = tipX + (float)(Math.Sin(angle) * lineLength);
            z = tipZ + (float)(Math.Cos(angle) * lineLength);
        }
    }
}
