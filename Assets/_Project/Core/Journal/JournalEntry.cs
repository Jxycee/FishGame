// Core/Journal/JournalEntry.cs: one journal entry (spec 12.10): what this player has seen and caught of a species.
// Plain serializable data (see MetaSave). Authority: local per peer.
using System;
using System.Collections.Generic;

namespace FishGame.Core
{
    [Serializable]
    public sealed class JournalEntry
    {
        public int Seen; public int CrewLanded; public int Caught;
        public int BestWeightG; public ushort BestLengthMm; public byte BestQuality;
        public List<string> SeenInSpots = new List<string>();
    }
}
