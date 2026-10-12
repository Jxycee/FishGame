// Core/Fishing/TensionBand.cs: tension feedback bands (spec 9.11 table, C 7.7).
namespace FishGame.Core
{
    public enum TensionBand : byte { Rest = 0, Bend = 1, Creak = 2, Warn = 3, Grace = 4 }  // <0.3, 0.3-0.6, 0.6-0.8, 0.8-1.0, >=1.0 in grace
}
