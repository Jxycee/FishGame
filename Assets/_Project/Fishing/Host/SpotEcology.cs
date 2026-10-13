// Fishing/Host/SpotEcology.cs - the shared world lookups of the host cast view and the owner cast marker (T15, M3-D15, M3-D16, C 2.8 #26).
// Authority: none (pure lookups over replicated world state and static baked data). The host's cast view and the owner's marker both use these
// functions, so the grid-water and casting-open gates agree on every peer: an honest cast at a grid edge is never host-rejected.
using System;
using FishGame.Core;
using FishGame.Data;

namespace FishGame.Fishing
{
    internal static class SpotEcology
    {
        // The zone and baked grid of the world's spot. False when anything is missing: no world or no state yet, an unknown spot id, no ecology
        // database, a spot index outside EcologyDatabase.Spots, or an unbaked grid (ZoneData.Grid null). Callers treat false as "casting
        // closed" (host: NotAllowedNow; owner: marker invalid), never as "no extra gate".
        public static bool TryResolve(ContentRegistry registry, IWorldContext world, out int zoneIndex, out ZoneData zone)
        {
            zoneIndex = -1;
            zone = null;
            if (registry == null || world == null || !world.HasState || string.IsNullOrEmpty(world.SpotId)) return false;
            EcologyDatabase eco = registry.Ecology;
            if (eco == null || !registry.TryGetSpot(world.SpotId, out _)) return false;
            return ResolveZone(eco, registry.SpotIndex(world.SpotId), out zoneIndex, out zone);
        }

        // The database half of TryResolve: a spot index outside EcologyDatabase.Spots, a zone index outside Zones or an unbaked grid is false.
        public static bool ResolveZone(EcologyDatabase eco, int spotIndex, out int zoneIndex, out ZoneData zone)
        {
            zoneIndex = -1;
            zone = null;
            if (eco == null || spotIndex < 0 || spotIndex >= eco.Spots.Count) return false;
            int zi = eco.Spots[spotIndex].ZoneIndex;
            if (zi < 0 || zi >= eco.Zones.Count || eco.Zones[zi].Grid == null) return false;
            zoneIndex = zi;
            zone = eco.Zones[zi];
            return true;
        }

        public static bool GridWater(ZoneData zone, float x, float z)
        {
            return zone != null && zone.Grid != null && zone.Grid.IsWater(x, z);
        }

        // The owner marker's two world gates in one place (C 2.8 #26): casting open from the replicated clock AND the landing on a grid water
        // cell. The host applies the same two terms separately in its cast view (CastingAllowed first, GridWater after the probe in ValidateCast).
        public static bool CastGatesOpen(IWorldContext world, ZoneData zone, float x, float z)
        {
            return world != null && world.CastingOpen && GridWater(zone, x, z);
        }

        // The world part of the host context (M3-D15). Rod tier, bait and depth are placeholders the ledger always overwrites.
        public static FishingContext WorldContext(IWorldContext world, ZoneData zone, int zoneIndex, float x, float z, float luck, int players)
        {
            return new FishingContext
            {
                ZoneIndex = zoneIndex,
                SubAreaIndex = zone.SubAreaIndexFor(zone.Grid.ShoreM(x, z)),   // not water -> +Infinity -> last band; GridWater false rejects anyway
                Depth = float.PositiveInfinity,
                Minute = world.Minute, Season = world.Season, Weather = world.Weather,
                RodTier = 0, Bait = BaitMods.Neutral(0),
                InHotspot = world.IsInHotspot(x, z),
                Luck = float.IsNaN(luck) ? 0f : Math.Max(-1f, Math.Min(1f, luck)),
                Events = EventMods.Neutral, Skills = SkillMods.Neutral,
                PlayersInSession = players < 1 ? 1 : players,
            };
        }
    }
}
