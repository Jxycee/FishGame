using System;
using FishGame.Core;
using NUnit.Framework;

namespace FishGame.Tests.EditMode
{
    public class SlotPaletteTests
    {
        [Test]
        public void SlotPalette_GetRgb_ReturnsContractColours()
        {
            float[,] expected =
            {
                { 0.22f, 0.52f, 0.90f },
                { 0.93f, 0.93f, 0.90f },
                { 0.45f, 0.82f, 0.35f },
                { 0.78f, 0.16f, 0.14f }
            };
            Assert.That(SlotPalette.Count, Is.EqualTo(4));
            for (int slot = 0; slot < SlotPalette.Count; slot++)
            {
                SlotPalette.GetRgb(slot, out float r, out float g, out float b);
                Assert.That(r, Is.EqualTo(expected[slot, 0]), "slot " + slot + " r");
                Assert.That(g, Is.EqualTo(expected[slot, 1]), "slot " + slot + " g");
                Assert.That(b, Is.EqualTo(expected[slot, 2]), "slot " + slot + " b");
            }

            SlotPalette.GetSkinRgb(out float sr, out float sg, out float sb);
            Assert.That(sr, Is.EqualTo(0.96f));
            Assert.That(sg, Is.EqualTo(0.80f));
            Assert.That(sb, Is.EqualTo(0.66f));
        }

        [Test]
        public void SlotPalette_GetRgb_OutOfRange_Throws()
        {
            Assert.Throws<ArgumentOutOfRangeException>(() => SlotPalette.GetRgb(-1, out _, out _, out _));
            Assert.Throws<ArgumentOutOfRangeException>(() => SlotPalette.GetRgb(SlotPalette.Count, out _, out _, out _));
        }
    }
}
