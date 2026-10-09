using System;
using System.Text;

namespace FishGame.Core
{
    // Connection payload sent in NetworkConfig.ConnectionData and decoded by the host's ConnectionApprover (contract 7.3, D3).
    // Pure data; the wire form is produced by ConnectPayloadCodec.
    public readonly struct ConnectPayload
    {
        public readonly int ProtocolVersion;
        public readonly ulong ContentHash;
        public readonly PlayerKey Key;
        public readonly string Name;

        public ConnectPayload(int protocolVersion, ulong contentHash, PlayerKey key, string name)
        {
            ProtocolVersion = protocolVersion;
            ContentHash = contentHash;
            Key = key;
            Name = name;
        }
    }

    // Wire format, little-endian:
    // byte FormatVersion (1) | int32 ProtocolVersion | uint64 ContentHash | byte Kind | byte idLen + idLen UTF-8 bytes | byte nameLen + nameLen UTF-8 bytes
    // Encode truncates Id to PlayerKey.MaxIdLength chars and Name to DisplayNameRules.MaxLength chars, then drops trailing chars
    // until each field is at most 64 UTF-8 bytes. A surrogate pair is never split. TryDecode never throws.
    public static class ConnectPayloadCodec
    {
        public const byte FormatVersion = 1;
        public const int MaxBytes = 255;

        private const int MaxFieldBytes = 64;
        private const int HeaderBytes = 1 + 4 + 8 + 1;    // format, protocol, hash, kind
        private const int MinBytes = HeaderBytes + 1 + 1; // plus two empty length-prefixed fields

        private static readonly UTF8Encoding Lenient = new UTF8Encoding(false, false);
        private static readonly UTF8Encoding Strict = new UTF8Encoding(false, true);

        public static byte[] Encode(in ConnectPayload payload)
        {
            byte[] id = FieldBytes(payload.Key.Id, PlayerKey.MaxIdLength);
            byte[] name = FieldBytes(payload.Name, DisplayNameRules.MaxLength);

            var data = new byte[HeaderBytes + 1 + id.Length + 1 + name.Length];
            int p = 0;
            data[p++] = FormatVersion;
            uint protocol = unchecked((uint)payload.ProtocolVersion);
            for (int i = 0; i < 4; i++) data[p++] = (byte)(protocol >> (8 * i));
            for (int i = 0; i < 8; i++) data[p++] = (byte)(payload.ContentHash >> (8 * i));
            data[p++] = (byte)payload.Key.Kind;
            data[p++] = (byte)id.Length;
            Buffer.BlockCopy(id, 0, data, p, id.Length);
            p += id.Length;
            data[p++] = (byte)name.Length;
            Buffer.BlockCopy(name, 0, data, p, name.Length);
            return data;
        }

        // False on null, too short or too long, wrong FormatVersion, unknown Kind, a length past the end or above 64,
        // trailing bytes, or invalid UTF-8.
        public static bool TryDecode(byte[] data, out ConnectPayload payload)
        {
            payload = default;
            if (data == null || data.Length < MinBytes || data.Length > MaxBytes) return false;

            int p = 0;
            if (data[p++] != FormatVersion) return false;

            uint protocol = 0;
            for (int i = 0; i < 4; i++) protocol |= (uint)data[p++] << (8 * i);
            ulong hash = 0;
            for (int i = 0; i < 8; i++) hash |= (ulong)data[p++] << (8 * i);

            byte kind = data[p++];
            if (kind > (byte)IdentityKind.Steam) return false;

            if (!TryReadField(data, ref p, out string id)) return false;
            if (!TryReadField(data, ref p, out string name)) return false;
            if (p != data.Length) return false;

            payload = new ConnectPayload(unchecked((int)protocol), hash, new PlayerKey((IdentityKind)kind, id), name);
            return true;
        }

        private static bool TryReadField(byte[] data, ref int p, out string value)
        {
            value = null;
            if (p >= data.Length) return false;
            int length = data[p++];
            if (length > MaxFieldBytes || p + length > data.Length) return false;
            try
            {
                value = Strict.GetString(data, p, length);
            }
            catch (ArgumentException)
            {
                return false; // DecoderFallbackException: invalid UTF-8
            }
            p += length;
            return true;
        }

        private static byte[] FieldBytes(string text, int maxChars)
        {
            string s = text ?? string.Empty;
            if (s.Length > maxChars) s = s.Substring(0, CutIndex(s, maxChars));
            byte[] bytes = Lenient.GetBytes(s);
            while (bytes.Length > MaxFieldBytes)
            {
                s = s.Substring(0, CutIndex(s, s.Length - 1));
                bytes = Lenient.GetBytes(s);
            }
            return bytes;
        }

        // Largest cut point <= length that does not split a surrogate pair.
        private static int CutIndex(string s, int length)
        {
            if (length > 0 && length < s.Length && char.IsHighSurrogate(s[length - 1]) && char.IsLowSurrogate(s[length])) return length - 1;
            return length;
        }
    }
}
