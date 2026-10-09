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
    }
}
