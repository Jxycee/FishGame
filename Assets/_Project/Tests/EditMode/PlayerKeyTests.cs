// Tests for FishGame.Core.PlayerKey (contract 7.3).
using FishGame.Core;
using NUnit.Framework;

namespace FishGame.Tests.EditMode
{
    public class PlayerKeyTests
    {
        [Test]
        public void PlayerKey_Equality_UsesKindAndOrdinalId()
        {
            var a = new PlayerKey(IdentityKind.Ugs, "abc");
            var same = new PlayerKey(IdentityKind.Ugs, "abc");
            var otherKind = new PlayerKey(IdentityKind.Local, "abc");
            var otherCase = new PlayerKey(IdentityKind.Ugs, "ABC");

            Assert.IsTrue(a == same);
            Assert.IsFalse(a != same);
            Assert.IsTrue(a.Equals(same));
            Assert.AreEqual(a.GetHashCode(), same.GetHashCode());
            Assert.IsTrue(a != otherKind);
            Assert.IsFalse(a.Equals(otherKind));
            Assert.IsTrue(a != otherCase, "Ids compare ordinally (case-sensitive).");
        }

        [Test]
        public void PlayerKey_EqualsObject_HandlesBoxedAndOtherTypes()
        {
            var a = new PlayerKey(IdentityKind.Ugs, "abc");
            object boxed = new PlayerKey(IdentityKind.Ugs, "abc");

            Assert.IsTrue(a.Equals(boxed));
            Assert.IsFalse(a.Equals("Ugs:abc"));
            Assert.IsFalse(a.Equals(null));
        }

        [Test]
        public void PlayerKey_IsValid_RejectsEmptyNullAndOverlong()
        {
            Assert.IsFalse(new PlayerKey(IdentityKind.Ugs, "").IsValid);
            Assert.IsFalse(new PlayerKey(IdentityKind.Ugs, null).IsValid);
            Assert.IsFalse(new PlayerKey(IdentityKind.Ugs, new string('x', PlayerKey.MaxIdLength + 1)).IsValid);
            Assert.IsTrue(new PlayerKey(IdentityKind.Ugs, new string('x', PlayerKey.MaxIdLength)).IsValid);
            Assert.IsTrue(new PlayerKey(IdentityKind.Local, "a").IsValid);
        }

        [Test]
        public void PlayerKey_ToString_IsKindColonId()
        {
            Assert.AreEqual("Local:abc", new PlayerKey(IdentityKind.Local, "abc").ToString());
            Assert.AreEqual("Ugs:p-1", new PlayerKey(IdentityKind.Ugs, "p-1").ToString());
        }

        [Test]
        public void PlayerKey_Default_IsInvalidLocalWithNullId()
        {
            var d = default(PlayerKey);
            Assert.AreEqual(IdentityKind.Local, d.Kind);
            Assert.IsNull(d.Id);
            Assert.IsFalse(d.IsValid);
            Assert.IsTrue(d == new PlayerKey(IdentityKind.Local, null));
            Assert.DoesNotThrow(() => d.GetHashCode());
            Assert.AreEqual("Local:", d.ToString());
        }
    }
}
