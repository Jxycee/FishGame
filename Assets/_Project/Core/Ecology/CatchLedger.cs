// Core/Ecology/CatchLedger.cs: crew-wide catch counts per species for catchLimitPerRun (spec 12.3), with reservations (M3-D14).
// Authority: host only (EcologyRunState owns it; FishingHostLedger reserves at roll, commits on Landed/ReleasedCreelFull, releases otherwise).
// Reservation semantics: two players can roll a limit-1 special at the same moment; Caught + Reserved >= limit blocks the second roll.
// No allocation on an existing key (indexer updates only); a new key may grow the dictionary.
using System;
using System.Collections.Generic;

namespace FishGame.Core
{
    public sealed class CatchLedger
    {
        private readonly Dictionary<ushort, int> _caught = new Dictionary<ushort, int>();
        private readonly Dictionary<ushort, int> _reserved = new Dictionary<ushort, int>();

        public int Caught(ushort speciesIndex)
        {
            return _caught.TryGetValue(speciesIndex, out int n) ? n : 0;
        }

        public int Reserved(ushort speciesIndex)
        {
            return _reserved.TryGetValue(speciesIndex, out int n) ? n : 0;
        }

        // limit < 0 (unlimited) is never at its limit; otherwise Caught + Reserved >= limit.
        public bool AtLimit(ushort speciesIndex, int limit)
        {
            if (limit < 0) return false;
            return (long)Caught(speciesIndex) + Reserved(speciesIndex) >= limit;
        }

        // No limit check: the host calls FishingResolver.Passes first.
        public void Reserve(ushort speciesIndex)
        {
            _reserved[speciesIndex] = Reserved(speciesIndex) + 1;
        }

        // Moves one reservation to caught. Throws InvalidOperationException (before any change) when nothing is reserved.
        public void Commit(ushort speciesIndex)
        {
            int r = Reserved(speciesIndex);
            if (r <= 0) throw new InvalidOperationException("Commit without a reservation for species " + speciesIndex + ".");
            _reserved[speciesIndex] = r - 1;
            _caught[speciesIndex] = Caught(speciesIndex) + 1;
        }

        // Drops one reservation. Throws InvalidOperationException (before any change) when nothing is reserved.
        public void Release(ushort speciesIndex)
        {
            int r = Reserved(speciesIndex);
            if (r <= 0) throw new InvalidOperationException("Release without a reservation for species " + speciesIndex + ".");
            _reserved[speciesIndex] = r - 1;
        }

        public void Clear()
        {
            _caught.Clear();
            _reserved.Clear();
        }
    }
}
