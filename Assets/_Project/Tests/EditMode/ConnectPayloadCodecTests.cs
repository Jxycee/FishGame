// Tests for FishGame.Core.ConnectPayload / ConnectPayloadCodec (contract 7.3, D3).
using System;
using FishGame.Core;
using NUnit.Framework;

namespace FishGame.Tests.EditMode
{
    public class ConnectPayloadCodecTests
    {
        private static ConnectPayload Sample()
        {
            return new ConnectPayload(7, 0xDEADBEEFCAFEF00DUL, new PlayerKey(IdentityKind.Ugs, "player-123"), "Captain");
        }

        [Test]
        public void ConnectPayloadCodec_RoundTrip_PreservesFields()
        {
            byte[] data = ConnectPayloadCodec.Encode(Sample());
            Assert.IsTrue(ConnectPayloadCodec.TryDecode(data, out ConnectPayload back));
            Assert.AreEqual(7, back.ProtocolVersion);
            Assert.AreEqual(0xDEADBEEFCAFEF00DUL, back.ContentHash);
            Assert.AreEqual(new PlayerKey(IdentityKind.Ugs, "player-123"), back.Key);
            Assert.AreEqual("Captain", back.Name);

            var negative = new ConnectPayload(-3, 0UL, new PlayerKey(IdentityKind.Steam, "s"), "");
            Assert.IsTrue(ConnectPayloadCodec.TryDecode(ConnectPayloadCodec.Encode(negative), out ConnectPayload negBack));
            Assert.AreEqual(-3, negBack.ProtocolVersion);
            Assert.AreEqual(IdentityKind.Steam, negBack.Key.Kind);
            Assert.AreEqual("", negBack.Name);

            var nulls = new ConnectPayload(1, 2UL, new PlayerKey(IdentityKind.Local, null), null);
            Assert.IsTrue(ConnectPayloadCodec.TryDecode(ConnectPayloadCodec.Encode(nulls), out ConnectPayload nullBack));
            Assert.AreEqual("", nullBack.Key.Id);
            Assert.AreEqual("", nullBack.Name);
        }

        [Test]
        public void ConnectPayloadCodec_Encode_IsLittleEndianWithFormatByteFirst()
        {
            byte[] data = ConnectPayloadCodec.Encode(new ConnectPayload(0x01020304, 0x1122334455667788UL, new PlayerKey(IdentityKind.Local, "a"), "b"));
            CollectionAssert.AreEqual(new byte[]
            {
                ConnectPayloadCodec.FormatVersion,
                0x04, 0x03, 0x02, 0x01,
                0x88, 0x77, 0x66, 0x55, 0x44, 0x33, 0x22, 0x11,
                (byte)IdentityKind.Local,
                1, (byte)'a',
                1, (byte)'b'
            }, data);
        }

        [Test]
        public void ConnectPayloadCodec_LongNameAndId_TruncatedUnderMaxBytes()
        {
            var payload = new ConnectPayload(1, 1UL, new PlayerKey(IdentityKind.Local, new string('x', 100)), "ABCDEFGHIJKLMNOPQRSTUV");
            byte[] data = ConnectPayloadCodec.Encode(payload);
            Assert.LessOrEqual(data.Length, ConnectPayloadCodec.MaxBytes);
            Assert.IsTrue(ConnectPayloadCodec.TryDecode(data, out ConnectPayload back));
            Assert.AreEqual(new string('x', PlayerKey.MaxIdLength), back.Key.Id);
            Assert.AreEqual("ABCDEFGHIJKLMNOP", back.Name);
        }

        [Test]
        public void ConnectPayloadCodec_Garbage_ReturnsFalse()
        {
            byte[] good = ConnectPayloadCodec.Encode(Sample());

            Assert.IsFalse(ConnectPayloadCodec.TryDecode(null, out _), "null");
            Assert.IsFalse(ConnectPayloadCodec.TryDecode(new byte[0], out _), "empty");
            Assert.IsFalse(ConnectPayloadCodec.TryDecode(new byte[] { 1, 2, 3 }, out _), "short");

            byte[] wrongFormat = (byte[])good.Clone();
            wrongFormat[0] = 2;
            Assert.IsFalse(ConnectPayloadCodec.TryDecode(wrongFormat, out _), "format version");

            byte[] unknownKind = (byte[])good.Clone();
            unknownKind[13] = 9;
            Assert.IsFalse(ConnectPayloadCodec.TryDecode(unknownKind, out _), "unknown kind");

            byte[] trailing = new byte[good.Length + 1];
            Buffer.BlockCopy(good, 0, trailing, 0, good.Length);
            Assert.IsFalse(ConnectPayloadCodec.TryDecode(trailing, out _), "trailing byte");

            byte[] truncated = new byte[good.Length - 1];
            Buffer.BlockCopy(good, 0, truncated, 0, truncated.Length);
            Assert.IsFalse(ConnectPayloadCodec.TryDecode(truncated, out _), "one byte short");

            byte[] idTooLong = (byte[])good.Clone();
            idTooLong[14] = 200;
            Assert.IsFalse(ConnectPayloadCodec.TryDecode(idTooLong, out _), "id length past the end");

            Assert.IsFalse(ConnectPayloadCodec.TryDecode(new byte[ConnectPayloadCodec.MaxBytes + 1], out _), "over MaxBytes");
        }

        // Review Focus 2: multi-byte UTF-8 and corrupted text bytes.
        [Test]
        public void ConnectPayloadCodec_MultiByteAndInvalidUtf8_HandledSafely()
        {
            // 64 x U+00E9 is 128 UTF-8 bytes; the id must shrink to 32 chars (64 bytes).
            string accented = new string('é', 64);
            // 15 x 'a' then U+1F600 (a surrogate pair): the 16-char name cut would split the pair, so it drops it.
            string emojiName = new string('a', 15) + "😀" + "b";
            byte[] data = ConnectPayloadCodec.Encode(new ConnectPayload(1, 1UL, new PlayerKey(IdentityKind.Ugs, accented), emojiName));
            Assert.LessOrEqual(data.Length, ConnectPayloadCodec.MaxBytes);
            Assert.IsTrue(ConnectPayloadCodec.TryDecode(data, out ConnectPayload back));
            Assert.AreEqual(new string('é', 32), back.Key.Id);
            Assert.AreEqual(new string('a', 15), back.Name);

            // Four-byte characters: 20 x U+1F600 is 40 chars and 80 bytes; the id keeps 16 whole pairs (64 bytes).
            var sb = new System.Text.StringBuilder();
            for (int i = 0; i < 20; i++) sb.Append("😀");
            byte[] emojiId = ConnectPayloadCodec.Encode(new ConnectPayload(1, 1UL, new PlayerKey(IdentityKind.Ugs, sb.ToString()), "n"));
            Assert.IsTrue(ConnectPayloadCodec.TryDecode(emojiId, out ConnectPayload emojiBack));
            Assert.AreEqual(32, emojiBack.Key.Id.Length);
            Assert.IsTrue(char.IsLowSurrogate(emojiBack.Key.Id[31]), "the last kept char closes a pair");

            // A name byte that is not valid UTF-8 (lone 0xFF) is rejected, not thrown.
            byte[] bad = ConnectPayloadCodec.Encode(new ConnectPayload(1, 1UL, new PlayerKey(IdentityKind.Ugs, "id"), "x"));
            bad[bad.Length - 1] = 0xFF;
            Assert.DoesNotThrow(() => ConnectPayloadCodec.TryDecode(bad, out _));
            Assert.IsFalse(ConnectPayloadCodec.TryDecode(bad, out _));
        }
    }
}
