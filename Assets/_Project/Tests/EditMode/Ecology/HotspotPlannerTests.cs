// Tests/EditMode/Ecology/HotspotPlannerTests.cs - T7: the spec 12.7 hotspot schedule (schedule-driven, monotonic, catch-up safe).
using System;
using System.Collections.Generic;
using FishGame.Core;
using NUnit.Framework;

namespace FishGame.Tests.EditMode
{
    public class HotspotPlannerTests
    {
        static EcologyTuning Tuning() => EcologyCoreTestData.Tuning();

        // Origin (0, 0), cell 1 m, shoreDm 10 for water and 0xFFFF otherwise, bits from PackBits.
        static ShoreGrid Grid(int w, int h, Func<int, bool> water, Func<int, bool> castable, Func<int, bool> reachable)
        {
            int n = w * h;
            var dm = new ushort[n];
            var c = new bool[n];
            var r = new bool[n];
            for (int i = 0; i < n; i++)
            {
                dm[i] = water(i) ? (ushort)10 : (ushort)0xFFFF;
                c[i] = castable(i);
                r[i] = reachable(i);
            }
            return new ShoreGrid(w, h, 1f, 0f, 0f, dm, ShoreGridMath.PackBits(c), ShoreGridMath.PackBits(r));
        }

        static ShoreGrid OpenGrid() => Grid(20, 20, i => true, i => true, i => true);

        struct Spawn
        {
            public int Start, End;
            public float X, Z;
        }

        // Steps the planner minute by minute (or by `stride`) and records each new hotspot once.
        static List<Spawn> Schedule(HotspotPlanner p, int stride)
        {
            var list = new List<Spawn>();
            for (int m = 0; m <= 1200; m += stride)
            {
                p.Advance(m);
                HotspotData h = p.Current;
                if (h.Active && (list.Count == 0 || list[list.Count - 1].Start != h.StartMinute))
                    list.Add(new Spawn { Start = h.StartMinute, End = h.EndMinute, X = h.X, Z = h.Z });
            }
            return list;
        }

        [Test]
        public void HotspotPlanner_FirstSpawn_BetweenSixtyAndOneTwentyMinutes()
        {
            EcologyTuning t = Tuning();
            ShoreGrid g = OpenGrid();
            int min = int.MaxValue, max = int.MinValue;
            for (ulong seed = 1; seed <= 500; seed++)
            {
                var p = new HotspotPlanner(in t, g, seed, 1);
                Assert.That(p.NextSpawnMinute, Is.InRange(60, 120), "seed " + seed);
                Assert.AreEqual(1, p.DrawCount);
                min = Math.Min(min, p.NextSpawnMinute); max = Math.Max(max, p.NextSpawnMinute);
            }
            Assert.AreEqual(60, min, "60 is drawn");
            Assert.AreEqual(120, max, "120 is drawn (inclusive max)");
        }

        [Test]
        public void HotspotPlanner_Duration_FortyFiveToSixty()
        {
            EcologyTuning t = Tuning();
            ShoreGrid g = OpenGrid();
            int min = int.MaxValue, max = int.MinValue;
            for (ulong seed = 1; seed <= 500; seed++)
            {
                var p = new HotspotPlanner(in t, g, seed, 1);
                int scheduled = p.NextSpawnMinute;
                Assert.IsFalse(p.Advance(scheduled - 1), "nothing before the scheduled minute");
                Assert.IsTrue(p.Advance(scheduled), "seed " + seed);
                Assert.IsTrue(p.Current.Active);
                Assert.AreEqual(scheduled, p.Current.StartMinute);
                int d = p.Current.EndMinute - p.Current.StartMinute;
                Assert.That(d, Is.InRange(45, 60));
                Assert.AreEqual(3, p.DrawCount, "delay, cell, duration");
                Assert.AreEqual(3f, p.Current.Radius);
                min = Math.Min(min, d); max = Math.Max(max, d);
            }
            Assert.AreEqual(45, min);
            Assert.AreEqual(60, max);
        }

        [Test]
        public void HotspotPlanner_AtMostOneAndGapRespected()
        {
            EcologyTuning t = Tuning();
            ShoreGrid g = OpenGrid();
            for (ulong seed = 1; seed <= 50; seed++)
            {
                var p = new HotspotPlanner(in t, g, seed, 1);
                var seen = new List<Spawn>();
                for (int m = 0; m <= 1200; m++)
                {
                    p.Advance(m);
                    HotspotData h = p.Current;
                    if (h.Active)
                    {
                        Assert.That(m, Is.InRange(h.StartMinute, h.EndMinute - 1), "Active only on [Start, End), seed " + seed + " minute " + m);
                        if (seen.Count == 0 || seen[seen.Count - 1].Start != h.StartMinute)
                            seen.Add(new Spawn { Start = h.StartMinute, End = h.EndMinute });
                    }
                    else
                    {
                        foreach (Spawn s in seen) Assert.IsFalse(m >= s.Start && m < s.End, "inactive inside a recorded hotspot");
                    }
                }
                Assert.GreaterOrEqual(seen.Count, 3, "at least three hotspots per day, seed " + seed);
                int prevEnd = 0;
                foreach (Spawn s in seen)
                {
                    Assert.That(s.Start, Is.InRange(prevEnd + 60, prevEnd + 120), "gap from the previous end, seed " + seed);
                    prevEnd = s.End;
                }
            }
        }

        [Test]
        public void HotspotPlanner_Cell_IsReachableCastableWater()
        {
            EcologyTuning t = Tuning();
            // 20x20: columns x < 10 are not reachable; row z == 5 is not water.
            ShoreGrid g = Grid(20, 20, i => i / 20 != 5, i => true, i => i % 20 >= 10);
            for (ulong seed = 1; seed <= 200; seed++)
            {
                var p = new HotspotPlanner(in t, g, seed, 1);
                p.Advance(p.NextSpawnMinute);
                HotspotData h = p.Current;
                Assert.IsTrue(h.Active);
                Assert.IsTrue(g.TryCell(h.X, h.Z, out int cx, out int cz));
                g.CellCentre(cx, cz, out float x, out float z);
                Assert.AreEqual(x, h.X, 1e-5f, "a cell centre");
                Assert.AreEqual(z, h.Z, 1e-5f, "a cell centre");
                Assert.IsTrue(g.IsWater(h.X, h.Z), "seed " + seed);
                Assert.IsTrue(g.IsCastable(cx, cz));
                Assert.IsTrue(g.IsReachable(cx, cz));
                Assert.AreNotEqual(5, cz);
            }
        }

        [Test]
        public void HotspotPlanner_NoReachableCells_NeverSpawns()
        {
            EcologyTuning t = Tuning();
            ShoreGrid unreachable = Grid(20, 20, i => true, i => true, i => false);
            foreach (ShoreGrid g in new[] { unreachable, null })
            {
                var p = new HotspotPlanner(in t, g, 7UL, 1);
                Assert.AreEqual(int.MaxValue, p.NextSpawnMinute);
                for (int m = 0; m <= 1200; m++) Assert.IsFalse(p.Advance(m), "minute " + m);
                Assert.IsFalse(p.Current.Active);
                Assert.AreEqual(0, p.DrawCount);

                p.ForceAt(3.5f, 4.5f, 30);
                Assert.IsTrue(p.Current.Active);
                Assert.IsTrue(p.Advance(90), "the forced hotspot ends");
                Assert.IsFalse(p.Current.Active);
                Assert.AreEqual(int.MaxValue, p.NextSpawnMinute, "ending a forced hotspot schedules nothing without candidates");
                Assert.AreEqual(0, p.DrawCount);
            }
        }

        [Test]
        public void HotspotPlanner_SameSeedAndDay_SameSchedule()
        {
            EcologyTuning t = Tuning();
            ShoreGrid g = OpenGrid();
            List<Spawn> by1 = Schedule(new HotspotPlanner(in t, g, 11UL, 1), 1);
            List<Spawn> by37 = Schedule(new HotspotPlanner(in t, g, 11UL, 1), 37);
            Assert.GreaterOrEqual(by1.Count, 3);
            Assert.AreEqual(by1.Count, by37.Count);
            for (int i = 0; i < by1.Count; i++)
            {
                Assert.AreEqual(by1[i].Start, by37[i].Start, "start " + i);
                Assert.AreEqual(by1[i].End, by37[i].End, "end " + i);
                Assert.AreEqual(by1[i].X, by37[i].X, "x " + i);
                Assert.AreEqual(by1[i].Z, by37[i].Z, "z " + i);
            }

            // One-call catch-up (T14 DevSetMinute and the F8 scrub): same end state and draw count as walking minute by minute.
            foreach (int m in new[] { 100, 600, 1199 })
            {
                var walked = new HotspotPlanner(in t, g, 11UL, 1);
                for (int k = 0; k <= m; k++) walked.Advance(k);
                var jumped = new HotspotPlanner(in t, g, 11UL, 1);
                jumped.Advance(m);
                Assert.AreEqual(walked.Current.Active, jumped.Current.Active, "active at " + m);
                Assert.AreEqual(walked.Current.StartMinute, jumped.Current.StartMinute, "start at " + m);
                Assert.AreEqual(walked.Current.EndMinute, jumped.Current.EndMinute, "end at " + m);
                Assert.AreEqual(walked.Current.X, jumped.Current.X, "x at " + m);
                Assert.AreEqual(walked.Current.Z, jumped.Current.Z, "z at " + m);
                Assert.AreEqual(walked.NextSpawnMinute, jumped.NextSpawnMinute, "next at " + m);
                Assert.AreEqual(walked.DrawCount, jumped.DrawCount, "draws at " + m);
            }

            List<Spawn> day2 = Schedule(new HotspotPlanner(in t, g, 11UL, 2), 1);
            bool differs = day2.Count != by1.Count;
            for (int i = 0; !differs && i < by1.Count; i++)
                differs = day2[i].Start != by1[i].Start || day2[i].X != by1[i].X || day2[i].Z != by1[i].Z;
            Assert.IsTrue(differs, "day 2 draws from its own stream");
        }

        // extra (T14 hardening, asked by the T5-T8 review): Advance(minute) clamps the minute to HotspotPlanner.MaxAdvanceMinute (a day plus
        // a day of margin), so a huge minute neither spins through ~10^7 spawn cycles nor overflows EndMinute into an endless loop.
        // Catches: no clamp (Advance(100000) walks past the clamp and ends in another state; Advance(int.MaxValue) would never return).
        [Test]
        public void HotspotPlanner_Advance_HugeMinuteClampedAndReturns()
        {
            EcologyTuning t = Tuning();
            ShoreGrid g = OpenGrid();
            Assert.That(HotspotPlanner.MaxAdvanceMinute, Is.GreaterThanOrEqualTo(DayClock.DayLengthMinutes), "a whole day still catches up");
            var clamped = new HotspotPlanner(in t, g, 5UL, 1);
            clamped.Advance(HotspotPlanner.MaxAdvanceMinute);
            foreach (int huge in new[] { 100000, int.MaxValue })   // 100000 first: on an unclamped planner it terminates and differs
            {
                var p = new HotspotPlanner(in t, g, 5UL, 1);
                p.Advance(huge);
                Assert.AreEqual(clamped.Current.Active, p.Current.Active, "active at " + huge);
                Assert.AreEqual(clamped.Current.StartMinute, p.Current.StartMinute, "start at " + huge);
                Assert.AreEqual(clamped.Current.EndMinute, p.Current.EndMinute, "end at " + huge);
                Assert.AreEqual(clamped.NextSpawnMinute, p.NextSpawnMinute, "next at " + huge);
                Assert.AreEqual(clamped.DrawCount, p.DrawCount, "draws at " + huge);
            }
        }

        // extra: pins the draw order of M3-D49 with a replica stream (delay, then cell, then duration; the next gap after the end).
        [Test]
        public void HotspotPlanner_DrawOrder_DelayThenCellThenDuration_HandReplayed()
        {
            EcologyTuning t = Tuning();
            ShoreGrid g = OpenGrid();
            var cells = new List<int>();
            g.CollectReachableWaterCells(cells);
            for (ulong seed = 1; seed <= 20; seed++)
            {
                Pcg32 r = Rng.Stream(seed, "hotspot", 3);
                var p = new HotspotPlanner(in t, g, seed, 3);
                int delay = r.Range(60, 121);
                Assert.AreEqual(delay, p.NextSpawnMinute, "(1) delay, seed " + seed);

                p.Advance(delay);
                int cell = cells[r.Range(0, cells.Count)];
                int duration = r.Range(45, 61);
                g.CellCentre(cell % g.Width, cell / g.Width, out float x, out float z);
                Assert.AreEqual(x, p.Current.X, "(2) cell, seed " + seed);
                Assert.AreEqual(z, p.Current.Z, "(2) cell, seed " + seed);
                Assert.AreEqual(delay + duration, p.Current.EndMinute, "(3) duration, seed " + seed);

                p.Advance(p.Current.EndMinute);
                int gap = r.Range(60, 121);
                Assert.AreEqual(delay + duration + gap, p.NextSpawnMinute, "(1) next gap after the end, seed " + seed);
                Assert.AreEqual(4, p.DrawCount);
            }
        }

        [Test]
        public void HotspotPlanner_ForceAt_ReplacesCurrentWithoutDraws()
        {
            EcologyTuning t = Tuning();
            var p = new HotspotPlanner(in t, OpenGrid(), 3UL, 1);
            Assert.AreEqual(1, p.DrawCount);

            HotspotData h = p.ForceAt(3.5f, -2.5f, 30);
            Assert.IsTrue(h.Active);
            Assert.AreEqual(3.5f, h.X);
            Assert.AreEqual(-2.5f, h.Z);
            Assert.AreEqual(3f, h.Radius);
            Assert.AreEqual(30, h.StartMinute);
            Assert.AreEqual(90, h.EndMinute);
            Assert.AreEqual(int.MaxValue, p.NextSpawnMinute, "the pending natural spawn is dropped");
            Assert.AreEqual(1, p.DrawCount, "ForceAt draws nothing");
            Assert.AreEqual(h.StartMinute, p.Current.StartMinute);

            Assert.IsTrue(p.Advance(90));
            Assert.IsFalse(p.Current.Active);
            Assert.AreEqual(2, p.DrawCount, "scheduling the next gap draws once");
            Assert.That(p.NextSpawnMinute, Is.InRange(150, 210));

            // A forced hotspot while another is active replaces it.
            var q = new HotspotPlanner(in t, OpenGrid(), 3UL, 1);
            q.Advance(q.NextSpawnMinute);
            Assert.IsTrue(q.Current.Active);
            float oldX = q.Current.X;
            q.ForceAt(oldX + 5f, 1f, q.Current.StartMinute + 1);
            Assert.AreEqual(oldX + 5f, q.Current.X);
            Assert.AreEqual(1f, q.Current.Z);
        }

        [Test]
        public void HotspotPlanner_Contains_RadiusThree()
        {
            var h = new HotspotData { Active = true, X = 0f, Z = 0f, Radius = 3f, StartMinute = 0, EndMinute = 60 };
            Assert.IsTrue(HotspotPlanner.Contains(in h, 3f, 0f));
            Assert.IsFalse(HotspotPlanner.Contains(in h, 3.01f, 0f));
            Assert.IsTrue(HotspotPlanner.Contains(in h, 2.1f, 2.1f));
            Assert.IsFalse(HotspotPlanner.Contains(in h, 2.13f, 2.13f));
            Assert.IsTrue(HotspotPlanner.Contains(in h, 0f, 0f));
            var off = new HotspotData { Active = false, X = 0f, Z = 0f, Radius = 3f };
            Assert.IsFalse(HotspotPlanner.Contains(in off, 0f, 0f));
            Assert.IsFalse(HotspotPlanner.Contains(in off, 1f, 1f));
        }

        [Test]
        public void HotspotPlanner_BadTuning_Throws()
        {
            EcologyTuning good = Tuning();
            EcologyTuning bad = Tuning();
            bad.HotspotDurationMin = 0;
            Assert.Throws<ArgumentException>(() => new HotspotPlanner(in bad, OpenGrid(), 1UL, 1));
            bad = Tuning(); bad.HotspotGapMax = 10;
            Assert.Throws<ArgumentException>(() => new HotspotPlanner(in bad, OpenGrid(), 1UL, 1));
            bad = Tuning(); bad.HotspotRadiusM = 0f;
            Assert.Throws<ArgumentException>(() => new HotspotPlanner(in bad, OpenGrid(), 1UL, 1));
            Assert.Throws<ArgumentOutOfRangeException>(() => new HotspotPlanner(in good, OpenGrid(), 1UL, 0));
        }
    }
}
