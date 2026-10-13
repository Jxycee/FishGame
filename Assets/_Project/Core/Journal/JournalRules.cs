// Core/Journal/JournalRules.cs: spec 12.10 journal updates and hint levels (M3-D25, B-D15). Pure; no Unity types.
// Authority: local per peer (JournalRecorder applies every CatchAnnounced to the local save; T20).
// Apply table (fish only; junk, PityCast and None never journal and create nothing):
//   Lost:                       Seen++, spot added once
//   Landed / ReleasedCreelFull: Seen++, spot added once, CrewLanded++; and when localCatcher: Caught++ and bests
//   Bests: only a strictly heavier fish replaces them (weight, length, quality together).
//   FirstCatch = Caught went 0 -> 1. PersonalBest = an earlier catch existed AND the weight beat the old best.
using System;
using System.Collections.Generic;

namespace FishGame.Core
{
    public static class JournalRules
    {
        public static bool ShouldJournal(AnnounceKind kind, bool isJunk)
        {
            if (isJunk) return false;
            return kind == AnnounceKind.Landed || kind == AnnounceKind.Lost || kind == AnnounceKind.ReleasedCreelFull;
        }

        // null -> 0; the highest satisfied: Caught >= 3 -> 3, CrewLanded >= 1 -> 2, Seen >= 1 -> 1, else 0.
        public static int HintLevel(JournalEntry e)
        {
            if (e == null) return 0;
            if (e.Caught >= EcologyConstants.JournalCaughtForLevel3) return 3;
            if (e.CrewLanded >= 1) return 2;
            if (e.Seen >= 1) return 1;
            return 0;
        }

        public static JournalUpdate Apply(MetaSave save, string speciesId, string spotId, AnnounceKind kind, bool isJunk, bool localCatcher,
                                          int weightG, ushort lengthMm, CatchQuality quality)
        {
            if (save == null) throw new ArgumentNullException(nameof(save));
            if (string.IsNullOrEmpty(speciesId)) throw new ArgumentException("speciesId is required.", nameof(speciesId));
            if (!ShouldJournal(kind, isJunk)) return default;

            if (save.Journal == null) save.Journal = new Dictionary<string, JournalEntry>();
            if (!save.Journal.TryGetValue(speciesId, out JournalEntry e) || e == null)
            {
                e = new JournalEntry();
                save.Journal[speciesId] = e;
            }
            if (e.SeenInSpots == null) e.SeenInSpots = new List<string>();

            var u = new JournalUpdate { Changed = true, LevelBefore = HintLevel(e) };
            e.Seen = Inc(e.Seen);
            if (!string.IsNullOrEmpty(spotId) && !ContainsOrdinal(e.SeenInSpots, spotId)) e.SeenInSpots.Add(spotId);

            if (kind == AnnounceKind.Landed || kind == AnnounceKind.ReleasedCreelFull)
            {
                e.CrewLanded = Inc(e.CrewLanded);
                if (localCatcher)
                {
                    int caughtBefore = e.Caught;
                    e.Caught = Inc(e.Caught);
                    bool heavier = weightG > e.BestWeightG;
                    u.FirstCatch = caughtBefore == 0;
                    u.PersonalBest = caughtBefore >= 1 && heavier;
                    if (heavier)
                    {
                        e.BestWeightG = weightG;
                        e.BestLengthMm = lengthMm;
                        e.BestQuality = (byte)quality;
                    }
                }
            }
            u.LevelAfter = HintLevel(e);
            return u;
        }

        // Entries with Caught >= 1 among the given journal species ids (spec: of 25). Entries not in ids are ignored; null ids are skipped.
        public static int CompletionCount(MetaSave save, IReadOnlyList<string> journalSpeciesIds)
        {
            if (save == null) throw new ArgumentNullException(nameof(save));
            if (journalSpeciesIds == null) throw new ArgumentNullException(nameof(journalSpeciesIds));
            if (save.Journal == null) return 0;
            int count = 0;
            for (int i = 0; i < journalSpeciesIds.Count; i++)
            {
                string id = journalSpeciesIds[i];
                if (id == null) continue;
                if (save.Journal.TryGetValue(id, out JournalEntry e) && e != null && e.Caught >= 1) count++;
            }
            return count;
        }

        private static int Inc(int v) => v < int.MaxValue ? v + 1 : v;

        private static bool ContainsOrdinal(List<string> list, string s)
        {
            for (int i = 0; i < list.Count; i++) if (string.Equals(list[i], s, StringComparison.Ordinal)) return true;
            return false;
        }
    }
}
