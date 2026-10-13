// Core/Render/ShaderGlobals.cs: global shader property names (C 5.3).
// Arrays are always pushed full length: _FGPalette 16, _Ripples 8 (RippleMath.Slots), _Waves 4 (C 2.8 #18).
// _Waves[i] = (DirectionDeg, Amplitude, Wavelength, Speed) for i = 0..2 and slot 3 all zero.
namespace FishGame.Core
{
    public static class ShaderGlobals
    {
        // Full array lengths (C 2.8 #18). The ripple array length is RippleMath.Slots (8).
        public const int PaletteLength = 16;
        public const int WavesLength = 4;

        public const string Palette = "_FGPalette";
        public const string Ripples = "_Ripples";
        public const string Waves = "_Waves";
        public const string WaterTime = "_WaterTime";
        public const string ToonShade = "_ToonShade";
        public const string ToonAmbient = "_ToonAmbient";
        public const string SkyTop = "_SkyTop";
        public const string SkyHorizon = "_SkyHorizon";
        public const string SkyGround = "_SkyGround";
    }
}
