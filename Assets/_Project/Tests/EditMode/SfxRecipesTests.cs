using System;
using FishGame.Core;
using NUnit.Framework;

namespace FishGame.Tests.EditMode
{
    public class SfxRecipesTests
    {
        private const ulong Seed = 1234UL;

        private static float Peak(float[] samples)
        {
            float peak = 0f;
            for (int i = 0; i < samples.Length; i++)
            {
                float a = Math.Abs(samples[i]);
                if (a > peak) peak = a;
            }
            return peak;
        }

        [Test]
        public void SfxRecipes_M1Table_MatchesContract()
        {
            string[] ids =
            {
                SfxIds.UiClick, SfxIds.UiConfirm, SfxIds.UiError,
                SfxIds.WindAmbient, SfxIds.WaterLap, SfxIds.BellRing,
            };
            Assert.AreEqual(ids.Length, SfxRecipes.M1.Count);
            for (int i = 0; i < ids.Length; i++)
            {
                Assert.AreEqual(ids[i], SfxRecipes.M1[i].Id, "order at index " + i);
            }

            Assert.IsTrue(SfxRecipes.TryGet(SfxIds.UiClick, out SfxRecipe click));
            Assert.AreEqual(0.08f, click.LengthSeconds, 1e-6f);
            Assert.IsFalse(click.Loop);
            Assert.AreEqual(AudioCategory.Ui, click.Category);
            Assert.AreEqual(0.6f, click.BaseVolume, 1e-6f);
            Assert.IsFalse(click.Spatial);
            Assert.AreEqual(0.03f, click.MinInterval, 1e-6f);

            Assert.IsTrue(SfxRecipes.TryGet(SfxIds.WindAmbient, out SfxRecipe wind));
            Assert.AreEqual(6.0f, wind.LengthSeconds, 1e-6f);
            Assert.IsTrue(wind.Loop);
            Assert.AreEqual(AudioCategory.Ambience, wind.Category);
            Assert.AreEqual(0.5f, wind.BaseVolume, 1e-6f);
            Assert.IsFalse(wind.Spatial);

            Assert.IsTrue(SfxRecipes.TryGet(SfxIds.WaterLap, out SfxRecipe lap));
            Assert.AreEqual(0.4f, lap.LengthSeconds, 1e-6f);
            Assert.IsFalse(lap.Loop);
            Assert.AreEqual(AudioCategory.Ambience, lap.Category);
            Assert.AreEqual(0.3f, lap.MinInterval, 1e-6f);

            Assert.IsTrue(SfxRecipes.TryGet(SfxIds.BellRing, out SfxRecipe bell));
            Assert.AreEqual(1.0f, bell.LengthSeconds, 1e-6f);
            Assert.AreEqual(AudioCategory.Sfx, bell.Category);
            Assert.AreEqual(0.9f, bell.BaseVolume, 1e-6f);
            Assert.IsTrue(bell.Spatial);
            Assert.AreEqual(30f, bell.MaxDistance, 1e-6f);
            Assert.AreEqual(0.25f, bell.MinInterval, 1e-6f);

            Assert.IsFalse(SfxRecipes.TryGet("Not_A_Cue", out _));
        }

        [Test]
        public void SfxRecipes_EveryM1Recipe_HasCorrectSampleCount()
        {
            foreach (SfxRecipe recipe in SfxRecipes.M1)
            {
                float[] samples = SfxRecipes.Generate(recipe.Id, SfxRecipes.SampleRate, Seed);
                int expected = (int)Math.Round(recipe.LengthSeconds * SfxRecipes.SampleRate);
                Assert.AreEqual(expected, SfxRecipes.SampleCount(recipe, SfxRecipes.SampleRate), recipe.Id);
                Assert.AreEqual(expected, samples.Length, recipe.Id);
            }
        }

        [Test]
        public void SfxRecipes_EveryM1Recipe_PeakBelowOne()
        {
            foreach (SfxRecipe recipe in SfxRecipes.M1)
            {
                float peak = Peak(SfxRecipes.Generate(recipe.Id, SfxRecipes.SampleRate, Seed));
                Assert.Less(peak, 1.0f, recipe.Id);
                Assert.Greater(peak, 0.5f, recipe.Id + " is almost silent");
            }
        }

        [Test]
        public void SfxRecipes_EveryM1Recipe_HasNoNaNOrInf()
        {
            foreach (SfxRecipe recipe in SfxRecipes.M1)
            {
                float[] samples = SfxRecipes.Generate(recipe.Id, SfxRecipes.SampleRate, Seed);
                int bad = -1;
                for (int i = 0; i < samples.Length; i++)
                {
                    if (float.IsNaN(samples[i]) || float.IsInfinity(samples[i]))
                    {
                        bad = i;
                        break;
                    }
                }
                Assert.AreEqual(-1, bad, recipe.Id + " has NaN or Inf at sample " + bad);
            }
        }

        [Test]
        public void SfxRecipes_Loops_HaveSmoothSeam()
        {
            int loops = 0;
            foreach (SfxRecipe recipe in SfxRecipes.M1)
            {
                if (!recipe.Loop) continue;
                loops++;
                float[] samples = SfxRecipes.Generate(recipe.Id, SfxRecipes.SampleRate, Seed);
                float seam = Math.Abs(samples[samples.Length - 1] - samples[0]);
                Assert.Less(seam, 0.05f, recipe.Id);
            }
            Assert.Greater(loops, 0, "at least one M1 recipe must be a loop");
        }

        [Test]
        public void SfxRecipes_SameSeed_IsDeterministic()
        {
            foreach (SfxRecipe recipe in SfxRecipes.M1)
            {
                float[] a = SfxRecipes.Generate(recipe.Id, SfxRecipes.SampleRate, Seed);
                float[] b = SfxRecipes.Generate(recipe.Id, SfxRecipes.SampleRate, Seed);
                Assert.AreEqual(a.Length, b.Length, recipe.Id);
                int firstDiff = -1;
                for (int i = 0; i < a.Length; i++)
                {
                    if (a[i] != b[i])
                    {
                        firstDiff = i;
                        break;
                    }
                }
                Assert.AreEqual(-1, firstDiff, recipe.Id + " differs at sample " + firstDiff);
            }
        }

        [Test]
        public void SfxRecipes_UnknownId_ThrowsArgumentException()
        {
            Assert.Throws<ArgumentException>(() => SfxRecipes.Generate("Not_A_Cue", SfxRecipes.SampleRate, Seed));
        }

        // ---- T11: M2 recipes -------------------------------------------------------------------------------------------------

        [Test]
        public void SfxRecipes_EveryM2Recipe_HasCorrectSampleCount()
        {
            Assert.AreEqual(12, SfxRecipes.M2.Count);
            foreach (SfxRecipe recipe in SfxRecipes.M2)
            {
                float[] samples = SfxRecipes.Generate(recipe.Id, SfxRecipes.SampleRate, Seed);
                int expected = (int)Math.Round(recipe.LengthSeconds * SfxRecipes.SampleRate);
                Assert.AreEqual(expected, SfxRecipes.SampleCount(recipe, SfxRecipes.SampleRate), recipe.Id);
                Assert.AreEqual(expected, samples.Length, recipe.Id);
            }
        }

        [Test]
        public void SfxRecipes_EveryM2Recipe_PeakBelowOne()
        {
            foreach (SfxRecipe recipe in SfxRecipes.M2)
            {
                float peak = Peak(SfxRecipes.Generate(recipe.Id, SfxRecipes.SampleRate, Seed));
                Assert.Less(peak, 1.0f, recipe.Id);
                Assert.Greater(peak, 0.5f, recipe.Id + " is almost silent");
            }
        }

        [Test]
        public void SfxRecipes_EveryM2Recipe_HasNoNaNOrInf()
        {
            foreach (SfxRecipe recipe in SfxRecipes.M2)
            {
                float[] samples = SfxRecipes.Generate(recipe.Id, SfxRecipes.SampleRate, Seed);
                int bad = -1;
                for (int i = 0; i < samples.Length; i++)
                {
                    if (float.IsNaN(samples[i]) || float.IsInfinity(samples[i]))
                    {
                        bad = i;
                        break;
                    }
                }
                Assert.AreEqual(-1, bad, recipe.Id + " has NaN or Inf at sample " + bad);
            }
        }

        [Test]
        public void SfxRecipes_M2Loops_HaveSmoothSeam()
        {
            int loops = 0;
            foreach (SfxRecipe recipe in SfxRecipes.M2)
            {
                if (!recipe.Loop) continue;
                loops++;
                float[] samples = SfxRecipes.Generate(recipe.Id, SfxRecipes.SampleRate, Seed);
                float seam = Math.Abs(samples[samples.Length - 1] - samples[0]);
                Assert.Less(seam, 0.05f, recipe.Id);
            }
            Assert.AreEqual(2, loops, "exactly Line_Creak and Dive_Thrum loop");
            Assert.IsTrue(SfxRecipes.TryGet(SfxIds.LineCreak, out SfxRecipe creak) && creak.Loop);
            Assert.IsTrue(SfxRecipes.TryGet(SfxIds.DiveThrum, out SfxRecipe thrum) && thrum.Loop);
        }

        [Test]
        public void SfxRecipes_AllIds_Unique()
        {
            Assert.AreEqual(18, SfxRecipes.All.Count);
            Assert.AreEqual(6, SfxRecipes.M1.Count);
            Assert.AreEqual(12, SfxRecipes.M2.Count);
            for (int i = 0; i < 6; i++) Assert.AreEqual(SfxRecipes.M1[i].Id, SfxRecipes.All[i].Id, "M1 first, index " + i);
            for (int i = 0; i < 12; i++) Assert.AreEqual(SfxRecipes.M2[i].Id, SfxRecipes.All[6 + i].Id, "then M2, index " + i);
            var seen = new System.Collections.Generic.HashSet<string>(StringComparer.Ordinal);
            foreach (SfxRecipe recipe in SfxRecipes.All)
            {
                Assert.IsTrue(seen.Add(recipe.Id), "duplicate id " + recipe.Id);
                Assert.IsTrue(SfxRecipes.TryGet(recipe.Id, out SfxRecipe found), "TryGet misses " + recipe.Id);
                Assert.AreEqual(recipe.Id, found.Id);
            }
            Assert.AreSame(SfxRecipes.All, SfxRecipes.All, "All is one cached list");
        }

        // Extra: pins the mixing seeds that the T14 cue table copies (T11.2 table). Catches a transposed column or a wrong id order.
        [Test]
        public void SfxRecipes_M2Table_MatchesPlan()
        {
            // id, length, loop, base, maxDist, minInterval, minDist (category Sfx and spatial for all twelve)
            object[][] rows =
            {
                new object[] { SfxIds.CastWhoosh, 0.35f, false, 0.70f, 30f, 0.10f, 1f },
                new object[] { SfxIds.SplashSmall, 0.25f, false, 0.70f, 30f, 0.08f, 1f },
                new object[] { SfxIds.SplashBig, 0.60f, false, 1.00f, 45f, 0.20f, 8f },
                new object[] { SfxIds.BitePlop, 0.20f, false, 0.70f, 30f, 0.10f, 1f },
                new object[] { SfxIds.NibbleTick, 0.03f, false, 0.50f, 20f, 0.30f, 1f },
                new object[] { SfxIds.ReelClick, 0.02f, false, 0.45f, 20f, 0.05f, 1f },
                new object[] { SfxIds.LineCreak, 0.50f, true, 0.60f, 25f, 0f, 1f },
                new object[] { SfxIds.DiveThrum, 1.00f, true, 0.70f, 30f, 0f, 1f },
                new object[] { SfxIds.Snap, 0.40f, false, 1.00f, 45f, 0.20f, 4f },
                new object[] { SfxIds.CatchFlop, 0.50f, false, 0.80f, 30f, 0.20f, 1f },
                new object[] { SfxIds.CatchCheer, 0.80f, false, 0.80f, 60f, 0.30f, 6f },
                new object[] { SfxIds.RodThunk, 0.15f, false, 0.60f, 20f, 0.10f, 1f },
            };
            Assert.AreEqual(rows.Length, SfxRecipes.M2.Count);
            for (int i = 0; i < rows.Length; i++)
            {
                SfxRecipe r = SfxRecipes.M2[i];
                string id = (string)rows[i][0];
                Assert.AreEqual(id, r.Id, "row " + i);
                Assert.AreEqual((float)rows[i][1], r.LengthSeconds, 1e-6f, id + " length");
                Assert.AreEqual((bool)rows[i][2], r.Loop, id + " loop");
                Assert.AreEqual(AudioCategory.Sfx, r.Category, id + " category");
                Assert.AreEqual((float)rows[i][3], r.BaseVolume, 1e-6f, id + " base");
                Assert.IsTrue(r.Spatial, id + " spatial");
                Assert.AreEqual((float)rows[i][4], r.MaxDistance, 1e-6f, id + " maxDistance");
                Assert.AreEqual((float)rows[i][5], r.MinInterval, 1e-6f, id + " minInterval");
                Assert.AreEqual((float)rows[i][6], r.MinDistance, 1e-6f, id + " minDistance");
            }
            foreach (SfxRecipe m1 in SfxRecipes.M1) Assert.AreEqual(1f, m1.MinDistance, 1e-6f, m1.Id + " keeps the 8-arg default MinDistance");

            var nine = new SfxRecipe("X", 1f, true, AudioCategory.Music, 0.3f, true, 12f, 0.4f, 7f);
            Assert.AreEqual(12f, nine.MaxDistance, 1e-6f, "ctor order: maxDistance is the 7th");
            Assert.AreEqual(0.4f, nine.MinInterval, 1e-6f, "ctor order: minInterval is the 8th");
            Assert.AreEqual(7f, nine.MinDistance, 1e-6f, "ctor order: minDistance is LAST (C-Q1)");
        }

        // Extra: no click at either end of a one-shot.
        [Test]
        public void SfxRecipes_M2OneShots_StartAndEndNearZero()
        {
            foreach (SfxRecipe recipe in SfxRecipes.M2)
            {
                if (recipe.Loop) continue;
                float[] samples = SfxRecipes.Generate(recipe.Id, SfxRecipes.SampleRate, Seed);
                Assert.Less(Math.Abs(samples[0]), 0.05f, recipe.Id + " start");
                Assert.Less(Math.Abs(samples[samples.Length - 1]), 0.05f, recipe.Id + " end");
            }
        }

        // Extra: every M2 recipe is deterministic for a seed, and a noise recipe follows the seeded stream.
        [Test]
        public void SfxRecipes_M2_DeterministicAndSeedDriven()
        {
            foreach (SfxRecipe recipe in SfxRecipes.M2)
            {
                float[] a = SfxRecipes.Generate(recipe.Id, SfxRecipes.SampleRate, Seed);
                float[] b = SfxRecipes.Generate(recipe.Id, SfxRecipes.SampleRate, Seed);
                CollectionAssert.AreEqual(a, b, recipe.Id + " must be deterministic for a seed");
            }
            float[] s1 = SfxRecipes.Generate(SfxIds.SplashSmall, SfxRecipes.SampleRate, 1UL);
            float[] s2 = SfxRecipes.Generate(SfxIds.SplashSmall, SfxRecipes.SampleRate, 2UL);
            CollectionAssert.AreNotEqual(s1, s2, "Splash_Small noise follows the seed");
        }
    }
}
