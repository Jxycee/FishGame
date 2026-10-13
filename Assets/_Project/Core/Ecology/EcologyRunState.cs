// Core/Ecology/EcologyRunState.cs: the host's session-level ecology state that survives Spot loads (M3-D17): run seed, season, day,
// cast counter and the crew-wide catch ledger. No weather track here: the track is a pure function of (runSeed, season, table), so
// DevWorldDriver rebuilds its WeatherDirector on every Spot load and gets the same segments.
// Authority: host only (EcologySession owns the instance; clients never hold one).
namespace FishGame.Core
{
    public sealed class EcologyRunState
    {
        // Season starts as the ROLLED season (the WeatherDirector.RollSeason formula, inlined because T7 comes later; B-D8), Day 1, counter 0.
        public EcologyRunState(ulong runSeed)
        {
            RunSeed = runSeed;
            Pcg32 rng = Rng.Stream(runSeed, EcologyStreams.Season, 0);
            Season = (Season)rng.Range(0, 4);
            Day = 1;
            HostCastCounter = 0;
            Catches = new CatchLedger();
        }

        public ulong RunSeed { get; }
        public Season Season { get; set; }
        public int Day { get; set; }                 // starts 1
        public uint HostCastCounter { get; set; }    // FishingHostLedger increments it (never restarts on a map switch)
        public CatchLedger Catches { get; }
    }
}
