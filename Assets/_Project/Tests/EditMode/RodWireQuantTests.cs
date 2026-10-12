using System;
using FishGame.Core;
using NUnit.Framework;

namespace FishGame.Tests.EditMode
{
    public class RodWireQuantTests
    {
        [Test]
        public void RodWireQuant_LateralAndTension_RoundTripWithinOneStep()
        {
            for (int i = -100; i <= 100; i++)
            {
                float l = i * 0.01f;
                float back = RodWireQuant.DequantizeLateral(RodWireQuant.QuantizeLateral(l));
                Assert.That(Math.Abs(back - l), Is.LessThanOrEqualTo(1f / 127f), "lateral " + l);
            }
            for (int i = 0; i <= 100; i++)
            {
                float t = i * 0.01f;
                float back = RodWireQuant.DequantizeTension(RodWireQuant.QuantizeTension(t));
                Assert.That(Math.Abs(back - t), Is.LessThanOrEqualTo(1f / 255f), "tension " + t);
            }
        }

        [Test]
        public void RodWireQuant_OutOfRange_Clamps()
        {
            Assert.That(RodWireQuant.QuantizeLateral(2f), Is.EqualTo((sbyte)127));
            Assert.That(RodWireQuant.QuantizeLateral(-2f), Is.EqualTo((sbyte)-127));
            Assert.That(RodWireQuant.QuantizeLateral(float.NaN), Is.EqualTo((sbyte)0));
            Assert.That(RodWireQuant.QuantizeTension(1.5f), Is.EqualTo((byte)255));
            Assert.That(RodWireQuant.QuantizeTension(-1f), Is.EqualTo((byte)0));
            Assert.That(RodWireQuant.QuantizeTension(float.NaN), Is.EqualTo((byte)0));
        }

        [Test]
        public void RodWireQuant_ExactValues()
        {
            Assert.That(RodWireQuant.QuantizeLateral(0.5f), Is.EqualTo((sbyte)64));
            Assert.That(RodWireQuant.QuantizeLateral(-0.5f), Is.EqualTo((sbyte)-64));
            Assert.That(RodWireQuant.QuantizeLateral(1f), Is.EqualTo((sbyte)127));
            Assert.That(RodWireQuant.QuantizeLateral(-1f), Is.EqualTo((sbyte)-127));
            Assert.That(RodWireQuant.QuantizeTension(0.5f), Is.EqualTo((byte)128));
            Assert.That(RodWireQuant.QuantizeTension(1f), Is.EqualTo((byte)255));
            Assert.That(RodWireQuant.DequantizeLateral(sbyte.MinValue), Is.EqualTo(-1f));
        }

        // Catches (T23 review): a flag bit reused or moved (FlagGrace must be a free bit of the existing Flags byte, so the payload size is
        // unchanged), and a flags byte that does not survive the RodStateData wire struct with grace set.
        [Test]
        public void RodWireQuant_FlagBits_AreDistinctAndGraceIs32()
        {
            byte[] flags =
            {
                RodWireQuant.FlagBurst, RodWireQuant.FlagReel, RodWireQuant.FlagDive, RodWireQuant.FlagExhausted, RodWireQuant.FlagRecall, RodWireQuant.FlagGrace,
            };
            Assert.That(RodWireQuant.FlagGrace, Is.EqualTo((byte)32));
            int seen = 0;
            foreach (byte f in flags)
            {
                Assert.That(f & (f - 1), Is.EqualTo(0), "one bit: " + f);
                Assert.That(seen & f, Is.EqualTo(0), "bit already used: " + f);
                seen |= f;
            }
            Assert.That(seen, Is.EqualTo(63), "bits 0..5");

            var a = new FishGame.Fishing.RodStateData { State = 4, Flags = (byte)(RodWireQuant.FlagReel | RodWireQuant.FlagGrace), TensionQ = 255 };
            var b = a;
            b.Flags = RodWireQuant.FlagReel;
            Assert.That(a.Equals(b), Is.False, "FlagGrace is part of the change detection");
            b.Flags = a.Flags;
            Assert.That(a.Equals(b), Is.True);
        }
    }
}
