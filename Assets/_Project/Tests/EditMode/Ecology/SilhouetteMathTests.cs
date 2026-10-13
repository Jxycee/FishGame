// Tests/EditMode/Ecology/SilhouetteMathTests.cs - T8: the journal icon outline (M3-D29, B-D16).
using System;
using FishGame.Core;
using NUnit.Framework;

namespace FishGame.Tests.EditMode
{
    public class SilhouetteMathTests
    {
        static readonly BodyShape[] Shapes = { BodyShape.Round, BodyShape.Long, BodyShape.Flat, BodyShape.Eel, BodyShape.Squid };

        [Test]
        public void SilhouetteMath_EveryBodyShape_ClosedAtTipsPositiveInside()
        {
            foreach (BodyShape shape in Shapes)
            {
                foreach (float k in new[] { 0.8f, 1f, 1.6f })
                {
                    string what = shape + " k " + k;
                    Assert.AreEqual(0f, SilhouetteMath.HalfHeight(shape, k, 0f), what + " head tip");
                    Assert.AreEqual(0f, SilhouetteMath.HalfHeight(shape, k, 1f), what + " tail tip");
                    Assert.AreEqual(0f, SilhouetteMath.HalfHeight(shape, k, -0.1f), what + " before the head");
                    Assert.AreEqual(0f, SilhouetteMath.HalfHeight(shape, k, 1.1f), what + " past the tail");
                    Assert.AreEqual(0f, SilhouetteMath.HalfHeight(shape, k, float.NaN), what + " NaN");

                    float max = 0f;
                    for (int i = 1; i <= 99; i++)
                    {
                        float h = SilhouetteMath.HalfHeight(shape, k, i / 100f);
                        Assert.IsFalse(float.IsNaN(h) || float.IsInfinity(h), what + " finite at " + i);
                        Assert.Greater(h, 0f, what + " positive inside at " + i);
                        max = Math.Max(max, h);
                    }
                    Assert.LessOrEqual(max, 1f / (2f * SilhouetteMath.AspectRatio(shape, k)) + 1e-4f, what + " bounded by the aspect ratio");
                }
            }

            foreach (float u in new[] { 0.1f, 0.3f, 0.5f, 0.9f })
                Assert.AreEqual(SilhouetteMath.HalfHeight(BodyShape.Round, 1f, u), SilhouetteMath.HalfHeight((BodyShape)99, 1f, u), 0f, "unknown shape uses Round");
            Assert.AreEqual(SilhouetteMath.AspectRatio(BodyShape.Round, 1f), SilhouetteMath.AspectRatio((BodyShape)99, 1f), 0f);
        }

        [Test]
        public void SilhouetteMath_EelLongerThanPuffer()
        {
            Assert.AreEqual(16f, SilhouetteMath.AspectRatio(BodyShape.Eel, 1.6f), 1e-4f);
            Assert.AreEqual(1.3333f, SilhouetteMath.AspectRatio(BodyShape.Round, 0.8f), 1e-3f);
            Assert.Greater(SilhouetteMath.AspectRatio(BodyShape.Eel, 1.6f), SilhouetteMath.AspectRatio(BodyShape.Round, 0.8f) * 4f);
            Assert.Greater(SilhouetteMath.AspectRatio(BodyShape.Long, 1f), SilhouetteMath.AspectRatio(BodyShape.Round, 1f));
            Assert.Greater(SilhouetteMath.AspectRatio(BodyShape.Round, 1.6f), SilhouetteMath.AspectRatio(BodyShape.Round, 0.8f), "k stretches the body");
        }

        [Test]
        public void SilhouetteMath_ShapeKIsClampedAndNaNSafe()
        {
            Assert.AreEqual(SilhouetteMath.AspectRatio(BodyShape.Round, 1f), SilhouetteMath.AspectRatio(BodyShape.Round, float.NaN), 0f);
            Assert.AreEqual(SilhouetteMath.AspectRatio(BodyShape.Round, 4f), SilhouetteMath.AspectRatio(BodyShape.Round, 100f), 0f);
            Assert.AreEqual(SilhouetteMath.AspectRatio(BodyShape.Round, 0.25f), SilhouetteMath.AspectRatio(BodyShape.Round, -3f), 0f);
        }
    }
}
