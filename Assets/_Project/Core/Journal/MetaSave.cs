// Core/Journal/MetaSave.cs: the persistent meta save (spec 12.10): schema version plus the journal keyed by species id.
// Plain serializable data with public fields and no Unity types: the Runtime SaveService serializes it with Newtonsoft and the key
// names come from these field names (M3-D30). Authority: local per peer (each machine keeps its own file).
using System;
using System.Collections.Generic;

namespace FishGame.Core
{
    [Serializable]
    public sealed class MetaSave
    {
        public int SchemaVersion = 1;
        public Dictionary<string, JournalEntry> Journal = new Dictionary<string, JournalEntry>();
    }
}
