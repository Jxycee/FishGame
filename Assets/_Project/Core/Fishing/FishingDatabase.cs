// Core/Fishing/FishingDatabase.cs: immutable bundle of rods, baits, profiles, species, junk and tuning (C 7.5).
// Index spaces follow ContentRegistry ordinal-sorted ids. The constructor copies every array, so later caller mutation
// has no effect. Authority: none (read-only data; built identically on every peer from the same content).
using System;
using System.Collections.Generic;

namespace FishGame.Core
{
    public sealed class FishingDatabase
    {
        private readonly FishingTuning _tuning;

        public FishingDatabase(RodStats[] rods, BaitStats[] baits, FishFightProfile[] profiles, FishSpeciesData[] species,
                               JunkData[] junk, in FishingTuning tuning)
        {
            if (rods == null) throw new ArgumentNullException(nameof(rods));
            if (baits == null) throw new ArgumentNullException(nameof(baits));
            if (profiles == null) throw new ArgumentNullException(nameof(profiles));
            if (species == null) throw new ArgumentNullException(nameof(species));
            if (junk == null) throw new ArgumentNullException(nameof(junk));
            if (rods.Length == 0) throw new ArgumentException("rods must not be empty", nameof(rods));
            if (baits.Length == 0) throw new ArgumentException("baits must not be empty", nameof(baits));
            if (profiles.Length == 0) throw new ArgumentException("profiles must not be empty", nameof(profiles));

            for (int i = 0; i < species.Length; i++)
            {
                if (species[i].FightProfileIndex >= profiles.Length)
                    throw new ArgumentException("species " + i + " has FightProfileIndex " + species[i].FightProfileIndex
                                                + " but there are only " + profiles.Length + " profiles", nameof(species));
            }

            int unlimited = -1;
            for (int i = 0; i < baits.Length; i++)
            {
                if (baits[i].Unlimited) { unlimited = i; break; }
            }
            if (unlimited < 0) throw new ArgumentException("at least one bait must be Unlimited (the bare hook)", nameof(baits));

            Rods = Array.AsReadOnly((RodStats[])rods.Clone());
            Baits = Array.AsReadOnly((BaitStats[])baits.Clone());
            Profiles = Array.AsReadOnly((FishFightProfile[])profiles.Clone());
            Species = Array.AsReadOnly((FishSpeciesData[])species.Clone());
            Junk = Array.AsReadOnly((JunkData[])junk.Clone());
            _tuning = tuning;
            UnlimitedBaitIndex = unlimited;
        }

        public IReadOnlyList<RodStats> Rods { get; }
        public IReadOnlyList<BaitStats> Baits { get; }
        public IReadOnlyList<FishFightProfile> Profiles { get; }
        public IReadOnlyList<FishSpeciesData> Species { get; }
        public IReadOnlyList<JunkData> Junk { get; }
        public ref readonly FishingTuning Tuning => ref _tuning;

        // First bait with Unlimited (the bare hook).
        public int UnlimitedBaitIndex { get; }
    }
}
