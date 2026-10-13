// Core/Render/PhaseBlend.cs: result of DayNightMath.Blend: blend From -> To by T (smoothstep weight of To). Authority: none (plain data).
namespace FishGame.Core
{
    public struct PhaseBlend { public int From, To; public float T; }   // T 0..1 smoothstep weight of To
}
