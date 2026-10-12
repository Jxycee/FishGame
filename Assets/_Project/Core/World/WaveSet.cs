// Core/World/WaveSet.cs: the three gentle water waves (spec 10.4, C 7.12). Pure data, no engine references.
// Units: DirectionDeg degrees, Amplitude metres, Wavelength metres, Speed metres per second.
// Direction convention (rulings C-Q4, F-Q1/Q2): 0 deg = +Z, 90 deg = +X, the same as the CastMath yaw.
// A wave travels along dir = (sin(deg), cos(deg)) in (x, z).
namespace FishGame.Core
{
    public struct Wave
    {
        public float DirectionDeg, Amplitude, Wavelength, Speed;
    }

    public struct WaveSet
    {
        public Wave W0, W1, W2;

        // All zero: a flat surface.
        public static readonly WaveSet Flat = default;
    }
}
