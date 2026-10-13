// Tests/EditMode/Ecology/WeatherDirectorTests.cs - T7: the spec 12.2 weather chain: season-weighted Markov rows, 35 pre-rolled segments,
// infinite day continuation, season roll.
using System;
using FishGame.Core;
using NUnit.Framework;

namespace FishGame.Tests.EditMode
{
    public class WeatherDirectorTests
    {
        static WeatherTableData Table() => EcologyCoreTestData.Weather();

        static readonly WeatherMask[] Singles = { WeatherMask.Clear, WeatherMask.Rain, WeatherMask.Fog };

        static void AssertSameTrack(WeatherDirector a, WeatherDirector b)
        {
            Assert.AreEqual(a.Track.Count, b.Track.Count);
            for (int i = 0; i < a.Track.Count; i++) Assert.AreEqual(a.Track[i], b.Track[i], "segment " + i);
        }

        [Test]
        public void WeatherDirector_SameSeed_SameThirtyFiveSegments()
        {
            WeatherTableData t = Table();
            var a = new WeatherDirector(in t, Season.Summer, 9UL);
            var b = new WeatherDirector(in t, Season.Summer, 9UL);
            Assert.AreEqual(35, a.Track.Count);
            AssertSameTrack(a, b);

            var c = new WeatherDirector(in t, Season.Summer, 10UL);
            bool differs = false;
            for (int i = 0; i < 35; i++) if (a.Track[i] != c.Track[i]) differs = true;
            Assert.IsTrue(differs, "another seed gives another week");
        }

        [Test]
        public void WeatherDirector_FirstSegment_DrawnFromClearRow()
        {
            WeatherTableData t = Table();
            int clear = 0, rain = 0, fog = 0;
            for (ulong s = 1; s <= 4000; s++)
            {
                var d = new WeatherDirector(in t, Season.Summer, s);
                Pcg32 rng = Rng.Stream(s, "weather", 0);
                Assert.AreEqual(WeatherDirector.Next(in t, Season.Summer, WeatherMask.Clear, ref rng), d.Track[0], "seed " + s);
                if (d.Track[0] == WeatherMask.Clear) clear++; else if (d.Track[0] == WeatherMask.Rain) rain++; else fog++;
            }
            Assert.AreEqual(0.670391, clear / 4000.0, 0.03);
            Assert.AreEqual(0.279330, rain / 4000.0, 0.03);
            Assert.AreEqual(0.050279, fog / 4000.0, 0.03);
        }

        // [season][current][next]
        static readonly float[][][] Expected =
        {
            new[] { new[] { 0.533333f, 0.333333f, 0.133333f }, new[] { 0.32f, 0.60f, 0.08f }, new[] { 0.363636f, 0.272727f, 0.363636f } },         // Spring
            new[] { new[] { 0.670391f, 0.279330f, 0.050279f }, new[] { 0.430108f, 0.537634f, 0.032258f }, new[] { 0.555556f, 0.277778f, 0.166667f } }, // Summer
            new[] { new[] { 0.60f, 0.25f, 0.15f }, new[] { 0.40f, 0.50f, 0.10f }, new[] { 0.40f, 0.20f, 0.40f } },                                  // Autumn
            new[] { new[] { 0.574163f, 0.239234f, 0.186603f }, new[] { 0.388350f, 0.485437f, 0.126214f }, new[] { 0.357143f, 0.178571f, 0.464286f } }, // Winter
        };

        [Test]
        public void WeatherDirector_RowProbabilities_SeasonMultipliersRenormalised()
        {
            WeatherTableData t = Table();
            var row = new float[3];
            for (int s = 0; s < 4; s++)
            {
                for (int c = 0; c < 3; c++)
                {
                    WeatherDirector.RowProbabilities(in t, (Season)s, Singles[c], row);
                    for (int n = 0; n < 3; n++) Assert.AreEqual(Expected[s][c][n], row[n], 1e-5f, "season " + s + " row " + c + " col " + n);
                    Assert.AreEqual(1f, row[0] + row[1] + row[2], 1e-6f, "row sums to 1");
                }
            }
            Assert.Throws<ArgumentException>(() => WeatherDirector.RowProbabilities(in t, Season.Summer, WeatherMask.Clear, new float[2]));
            WeatherTableData t2 = Table();
            Assert.Throws<ArgumentException>(() => WeatherDirector.RowProbabilities(in t2, Season.Summer, WeatherMask.Rain | WeatherMask.Storm, row));
            Assert.Throws<ArgumentException>(() => WeatherDirector.RowProbabilities(in t2, Season.Summer, WeatherMask.None, row));
        }

        [Test]
        public void WeatherDirector_TenThousandSteps_MatchMarkovRowsWithinTolerance()
        {
            WeatherTableData t = Table();
            for (int s = 0; s < 4; s++)
            {
                Pcg32 rng = new Pcg32(5);
                WeatherMask cur = WeatherMask.Clear;
                var visits = new int[3];
                var next = new int[3, 3];
                for (int i = 0; i < 10000; i++)
                {
                    WeatherMask nx = WeatherDirector.Next(in t, (Season)s, cur, ref rng);
                    int ci = EcologyMasks.WeatherColumn(cur), ni = EcologyMasks.WeatherColumn(nx);
                    visits[ci]++; next[ci, ni]++;
                    cur = nx;
                }
                for (int c = 0; c < 3; c++)
                {
                    Assert.Greater(visits[c], 100, "season " + s + " row " + c + " visited enough");
                    for (int n = 0; n < 3; n++)
                    {
                        double p = Expected[s][c][n], f = next[c, n] / (double)visits[c];
                        double tol = 4 * Math.Sqrt(p * (1 - p) / visits[c]) + 0.005;
                        Assert.AreEqual(p, f, tol, "season " + s + " " + c + "->" + n + " over " + visits[c] + " visits");
                    }
                }
            }
        }

        [Test]
        public void WeatherDirector_OnlyClearRainFog()
        {
            WeatherTableData t = Table();
            for (ulong seed = 1; seed <= 200; seed++)
            {
                for (int s = 0; s < 4; s++)
                {
                    var d = new WeatherDirector(in t, (Season)s, seed);
                    d.EnsureDay(10);
                    Assert.AreEqual(50, d.Track.Count);
                    foreach (WeatherMask m in d.Track)
                        Assert.IsTrue(m == WeatherMask.Clear || m == WeatherMask.Rain || m == WeatherMask.Fog, "seed " + seed + " season " + s + " got " + m);
                }
            }
        }

        [Test]
        public void WeatherDirector_InfiniteDay_ContinuesFromPreviousSegmentDeterministically()
        {
            WeatherTableData t = Table();
            var a = new WeatherDirector(in t, Season.Autumn, 77UL);
            var b = new WeatherDirector(in t, Season.Autumn, 77UL);
            WeatherMask[] first35 = new WeatherMask[35];
            for (int i = 0; i < 35; i++) first35[i] = a.Track[i];

            WeatherMask atA = a.At(10, 0);                                           // grows straight to day 10
            b.EnsureDay(8); b.EnsureDay(9); b.EnsureDay(10);                         // grows one day at a time
            Assert.AreEqual(50, a.Track.Count);
            Assert.AreEqual(50, b.Track.Count);
            AssertSameTrack(a, b);
            Assert.AreEqual(atA, b.At(10, 0));
            for (int i = 0; i < 35; i++) Assert.AreEqual(first35[i], a.Track[i], "planned week unchanged at " + i);

            Pcg32 rng = Rng.Stream(77UL, "weather", 8);
            Assert.AreEqual(WeatherDirector.Next(in t, Season.Autumn, a.Track[34], ref rng), a.Track[35], "day 8 continues from segment 34 on stream 8");
            Assert.Throws<ArgumentOutOfRangeException>(() => a.EnsureDay(0));
            Assert.Throws<ArgumentOutOfRangeException>(() => a.EnsureDay(10001));
        }

        [Test]
        public void WeatherDirector_RollSeason_DeterministicAndRoughlyUniform()
        {
            Assert.AreEqual(WeatherDirector.RollSeason(123UL), WeatherDirector.RollSeason(123UL));
            var counts = new int[4];
            for (ulong s = 1; s <= 4000; s++) counts[(int)WeatherDirector.RollSeason(s)]++;
            for (int i = 0; i < 4; i++) Assert.That(counts[i], Is.InRange(850, 1150), "season " + i);
            for (ulong s = 1; s <= 100; s++) Assert.AreEqual(WeatherDirector.RollSeason(s), new EcologyRunState(s).Season, "seed " + s);
        }

        [Test]
        public void WeatherDirector_At_UsesSegmentOfMinute()
        {
            WeatherTableData t = Table();
            var d = new WeatherDirector(in t, Season.Spring, 4UL);
            Assert.AreEqual(d.Track[0], d.At(1, 0));
            Assert.AreEqual(d.Track[0], d.At(1, 239));
            Assert.AreEqual(d.Track[1], d.At(1, 240));
            Assert.AreEqual(d.Track[4], d.At(1, 1199));
            Assert.AreEqual(d.Track[4], d.At(1, 1200));
            Assert.AreEqual(d.Track[5], d.At(2, 0));
            Assert.AreEqual(d.Track[34], d.At(7, 1199));
            Assert.Throws<ArgumentOutOfRangeException>(() => d.At(0, 0));
        }

        [Test]
        public void WeatherDirector_BadTable_Throws()
        {
            WeatherTableData good = Table();
            WeatherTableData shortBase = new WeatherTableData { Base = new float[8], SeasonColumnMult = good.SeasonColumnMult };
            Assert.Throws<ArgumentException>(() => new WeatherDirector(in shortBase, Season.Spring, 1UL));
            WeatherTableData zeroRow = Table();
            zeroRow.Base[0] = 0f; zeroRow.Base[1] = 0f; zeroRow.Base[2] = 0f;
            Assert.Throws<ArgumentException>(() => new WeatherDirector(in zeroRow, Season.Spring, 1UL));
            WeatherTableData zeroMult = Table();
            zeroMult.SeasonColumnMult[3] = 0f;
            Assert.Throws<ArgumentException>(() => new WeatherDirector(in zeroMult, Season.Spring, 1UL));
            WeatherTableData negative = Table();
            negative.Base[4] = -0.1f;
            Assert.Throws<ArgumentException>(() => new WeatherDirector(in negative, Season.Spring, 1UL));
            Assert.Throws<ArgumentOutOfRangeException>(() => new WeatherDirector(in good, (Season)4, 1UL));
            Assert.DoesNotThrow(() => new WeatherDirector(in good, Season.Winter, 1UL), "control");
        }
    }
}
