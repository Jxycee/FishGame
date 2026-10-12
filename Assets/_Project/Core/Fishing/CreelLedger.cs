// Core/Fishing/CreelLedger.cs: host slot-keyed creel store (R46: survives disconnect and rejoin within the session).
// Authority: host only; lives on SessionState (T16). Slots are 0..slotCount-1; any other slot throws ArgumentOutOfRangeException.
using System;
using System.Collections.Generic;

namespace FishGame.Core
{
    public sealed class CreelLedger
    {
        private readonly List<CreelItem>[] _items;
        private readonly int[] _lastEntryId;   // per-slot counter, never reset (not even by SellAll)
        private readonly int[] _coins;

        public CreelLedger(int slotCount)
        {
            if (slotCount < 1) throw new ArgumentOutOfRangeException(nameof(slotCount));
            _items = new List<CreelItem>[slotCount];
            _lastEntryId = new int[slotCount];
            _coins = new int[slotCount];
            for (int i = 0; i < slotCount; i++) _items[i] = new List<CreelItem>();
        }

        public IReadOnlyList<CreelItem> Items(byte slot)
        {
            return _items[Check(slot)];
        }

        public CreelSnapshot Snapshot(byte slot, int maxEntries, int maxWeightG)
        {
            List<CreelItem> list = _items[Check(slot)];
            int weight = 0;
            for (int i = 0; i < list.Count; i++) weight += list[i].WeightG;
            return new CreelSnapshot(list.Count, weight, maxEntries, maxWeightG);
        }

        // Assigns EntryId (per slot, monotonic, starts at 1) and returns the stored item.
        public CreelItem Add(byte slot, CreelItem item)
        {
            int s = Check(slot);
            int id = _lastEntryId[s] + 1;
            if (id > ushort.MaxValue) id = 1;   // unreachable in M2 (a creel holds a handful of entries); keeps ids non-zero
            _lastEntryId[s] = id;
            item.EntryId = (ushort)id;
            _items[s].Add(item);
            return item;
        }

        // Sum of ValueCoins; clears the slot and adds the sum to that slot debug wallet.
        public int SellAll(byte slot)
        {
            int s = Check(slot);
            int sum = 0;
            List<CreelItem> list = _items[s];
            for (int i = 0; i < list.Count; i++) sum += list[i].ValueCoins;
            list.Clear();
            _coins[s] += sum;
            return sum;
        }

        // Slot takeover (T16-R1): a different persistent identity now holds the slot, so it starts with an empty creel and an empty debug
        // wallet. The per-slot EntryId counter keeps counting (ids only need to be unique within the slot's current creel).
        public void ClearSlot(byte slot)
        {
            int s = Check(slot);
            _items[s].Clear();
            _coins[s] = 0;
        }

        // M2 debug wallet (sum of sales).
        public int DebugCoins(byte slot)
        {
            return _coins[Check(slot)];
        }

        private int Check(byte slot)
        {
            if (slot >= _items.Length) throw new ArgumentOutOfRangeException(nameof(slot));
            return slot;
        }
    }
}
