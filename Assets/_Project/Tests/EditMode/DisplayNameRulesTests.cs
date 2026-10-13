// Tests for FishGame.Core.DisplayNameRules (contract 7.3).
using FishGame.Core;
using NUnit.Framework;

namespace FishGame.Tests.EditMode
{
    public class DisplayNameRulesTests
    {
        [Test]
        public void DisplayNameRules_Sanitize_StripsControlTrimsAndTruncates()
        {
            Assert.AreEqual("Bob", DisplayNameRules.Sanitize("  Bob\u0007 \t", 0));
            Assert.AreEqual("AB", DisplayNameRules.Sanitize("A\nB", 0));
            Assert.AreEqual("ABCDEFGHIJKLMNOP", DisplayNameRules.Sanitize("ABCDEFGHIJKLMNOPQRSTUVWXYZ", 0));
            Assert.AreEqual("ABCDEFGHIJKLMNO", DisplayNameRules.Sanitize("ABCDEFGHIJKLMNO  Z", 0), "TrimEnd after the cut");
            Assert.AreEqual("Captain Hook", DisplayNameRules.Sanitize("Captain Hook", 2));
            Assert.AreEqual(16, DisplayNameRules.MaxLength);
        }

        [Test]
        public void DisplayNameRules_Sanitize_Empty_UsesSlotFallback()
        {
            Assert.AreEqual("Fisher 1", DisplayNameRules.Sanitize(null, 0));
            Assert.AreEqual("Fisher 1", DisplayNameRules.Sanitize("", 0));
            Assert.AreEqual("Fisher 4", DisplayNameRules.Sanitize("   ", 3));
            Assert.AreEqual("Fisher 3", DisplayNameRules.Sanitize("\u0001\u0002", 2));
            Assert.AreEqual("Fisher 2", DisplayNameRules.Fallback(1));
        }

        [Test]
        public void DisplayNameRules_Sanitize_DoesNotSplitSurrogatePair()
        {
            // 15 x 'a' + U+1F600 (two UTF-16 units) + 'b' is 18 chars; cutting at 16 would leave a lone high surrogate.
            string result = DisplayNameRules.Sanitize(new string('a', 15) + "😀" + "b", 0);
            Assert.AreEqual(new string('a', 15), result);

            // A pair that ends exactly at char 16 is kept whole.
            string whole = DisplayNameRules.Sanitize(new string('a', 14) + "😀" + "b", 0);
            Assert.AreEqual(new string('a', 14) + "😀", whole);
        }

        // Catches: a plain Substring(0, max) (splits the pair); an off-by-one that keeps the high surrogate alone; null handling.
        [Test]
        public void DisplayNameRules_Truncate_NeverSplitsSurrogatePair()
        {
            string fish = char.ConvertFromUtf32(0x1F41F);   // two UTF-16 chars
            Assert.AreEqual("ab", DisplayNameRules.Truncate("abc", 2));
            Assert.AreEqual("a", DisplayNameRules.Truncate("a" + fish + "b", 2), "the pair at index 1-2 is not split");
            Assert.AreEqual("a" + fish, DisplayNameRules.Truncate("a" + fish + "b", 3));
            Assert.AreEqual("", DisplayNameRules.Truncate(fish, 1), "a leading pair with max 1");
            Assert.AreEqual("ab", DisplayNameRules.Truncate("ab", 5));
            Assert.AreEqual("", DisplayNameRules.Truncate(null, 4));
            Assert.AreEqual("", DisplayNameRules.Truncate("abc", 0));
            Assert.Throws<System.ArgumentOutOfRangeException>(() => DisplayNameRules.Truncate("a", -1));
        }

        // Catches: the two paths drifting apart again (the old SessionText.Name kept a lone surrogate, which encodes as a 3-byte U+FFFD
        // replacement); FitBytes splitting a pair.
        [Test]
        public void DisplayNameRules_Truncate_SharedByNameWireAndSessionText()
        {
            string fish = char.ConvertFromUtf32(0x1F41F);      // 4 UTF-8 bytes
            string day = ((char)0x65E5).ToString();            // 3 UTF-8 bytes
            string lone = ((char)0xD83D).ToString();           // a lone high surrogate
            var cases = new[]
            {
                (new string('a', 16), new string('a', 16)),                                  // unchanged
                (string.Concat(System.Linq.Enumerable.Repeat(fish, 8)), string.Concat(System.Linq.Enumerable.Repeat(fish, 7))),   // 32 -> 28 bytes
                (string.Concat(System.Linq.Enumerable.Repeat(day, 10)), string.Concat(System.Linq.Enumerable.Repeat(day, 9))),    // 30 -> 27 bytes
                ("ab" + lone + "c", "abc"),                                                  // the lone surrogate is dropped
                ("", ""),
            };
            foreach ((string input, string expected) in cases)
            {
                string wire = FishGame.Gameplay.NameWire.ToFixed(input).ToString();
                string session = FishGame.Gameplay.SessionText.Name(input).ToString();
                Assert.AreEqual(expected, wire, "NameWire.ToFixed");
                Assert.AreEqual(expected, session, "SessionText.Name");
            }

            Assert.AreEqual("a", FishGame.Gameplay.SessionText.FitBytes("a" + fish, 4), "5 bytes do not fit 4: the pair goes as a unit");
        }
    }
}
