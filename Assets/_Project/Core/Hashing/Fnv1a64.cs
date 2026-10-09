// Core/Hashing/Fnv1a64.cs: FNV-1a 64-bit. Mutable struct; use it as a local variable (var h = Fnv1a64.Create();).
using System;
using System.Text;

namespace FishGame.Core
{
    public struct Fnv1a64
    {
        public const ulong OffsetBasis = 14695981039346656037UL;
        public const ulong Prime = 1099511628211UL;

        private ulong _value;

        public static Fnv1a64 Create()
        {
            return new Fnv1a64 { _value = OffsetBasis };
        }

        public ulong Value => _value;

        public void Append(byte b)
        {
            unchecked
            {
                _value ^= b;
                _value *= Prime;
            }
        }

        public void Append(byte[] bytes)
        {
            if (bytes == null) throw new ArgumentNullException(nameof(bytes));
            for (int i = 0; i < bytes.Length; i++) Append(bytes[i]);
        }

        // UTF-8 bytes of s; null is treated as "".
        public void Append(string s)
        {
            Append(Encoding.UTF8.GetBytes(s ?? string.Empty));
        }

        public void AppendInt32LE(int value)
        {
            Append((byte)(value & 0xFF));
            Append((byte)((value >> 8) & 0xFF));
            Append((byte)((value >> 16) & 0xFF));
            Append((byte)((value >> 24) & 0xFF));
        }

        public static ulong Hash(string s)
        {
            var h = Create();
            h.Append(s);
            return h.Value;
        }

        public static ulong Hash(byte[] bytes)
        {
            var h = Create();
            h.Append(bytes);
            return h.Value;
        }
    }
}
