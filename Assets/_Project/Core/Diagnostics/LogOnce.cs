// Core/Diagnostics/LogOnce.cs: "log this key only the first time" gate. Pure C#, no engine references.
// Authority: none (local helper). Main thread only; not thread-safe.
using System;
using System.Collections.Generic;

namespace FishGame.Core
{
    public sealed class LogOnce
    {
        private readonly HashSet<string> _seen = new HashSet<string>(StringComparer.Ordinal);

        // True the first time a key is seen (ordinal compare); a null key is treated as "".
        public bool ShouldLog(string key)
        {
            return _seen.Add(key ?? "");
        }

        public void Reset()
        {
            _seen.Clear();
        }

        // Number of distinct keys seen since construction or Reset.
        public int Count => _seen.Count;
    }
}
