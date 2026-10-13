// Core/Ecology/EcologyStreams.cs: Rng stream ids for the ecology systems (2.8 #25). Each system owns its own id, so a draw added in
// one system never shifts another's rolls. Authority: none (constants).
namespace FishGame.Core
{
    public static class EcologyStreams
    {
        public const string Season = "season";     // index 0
        public const string Weather = "weather";   // index 0 for the 35 planned segments; index d for infinite day d >= 8
        public const string Hotspot = "hotspot";   // index = day
    }
}
