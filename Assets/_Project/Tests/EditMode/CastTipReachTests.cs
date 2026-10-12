// R67 (spec 3.2 / 9.3): the cast distance is measured from the rod tip, so a minimum cast leaves the owner a real fight line.
// Before R67 the landing was root + distance and the owner's FightSim started with TipToBobber = distance - ~2.5 m (the tip reach), so casts
// under ~4.4 m started with a line <= landDistance and landed on the first reel tick as clean catches. These tests model the tip as the
// rest-pose point castTipReach ahead of the root (look pitch 0) and run the real RodStateMachine / FightSim on the real shipped assets.
using System;
using System.Collections.Generic;
using System.Globalization;
using FishGame.Core;
using FishGame.Data;
using FishGame.EditorTools;
using NUnit.Framework;
using UnityEditor;

namespace FishGame.Tests.EditMode
{
    public class CastTipReachTests
    {
        const float Eps = 1e-4f;

        sealed class Loaded
        {
            public FishingTuning T;
            public readonly List<string> RodIds = new List<string>();
            public readonly List<RodStats> Rods = new List<RodStats>();
            public readonly List<string> ProfileIds = new List<string>();
            public readonly List<FishFightProfile> Profiles = new List<FishFightProfile>();
        }

        static Loaded LoadShipped()
        {
            var d = new Loaded();
            var tuning = AssetDatabase.LoadAssetAtPath<FishingTuningDef>(AssetPaths.FishingTuningAsset);
            Assert.IsNotNull(tuning, "missing " + AssetPaths.FishingTuningAsset + ": run Tools/unity.ps1 build first");
            d.T = tuning.ToCore();
            foreach (string guid in AssetDatabase.FindAssets("t:RodDef", new[] { AssetPaths.RodsFolder }))
            {
                var r = AssetDatabase.LoadAssetAtPath<RodDef>(AssetDatabase.GUIDToAssetPath(guid));
                d.RodIds.Add(r.Id);
                d.Rods.Add(r.ToCore());
            }
            foreach (string guid in AssetDatabase.FindAssets("t:FightProfileDef", new[] { AssetPaths.FightProfilesFolder }))
            {
                var p = AssetDatabase.LoadAssetAtPath<FightProfileDef>(AssetDatabase.GUIDToAssetPath(guid));
                d.ProfileIds.Add(p.Id);
                d.Profiles.Add(p.ToCore());
            }
            Assert.That(d.Rods.Count, Is.GreaterThanOrEqualTo(4), "shipped rods");
            Assert.That(d.Profiles.Count, Is.GreaterThanOrEqualTo(5), "shipped fight profiles");
            return d;
        }

        // The geometry both sides share: origin (0,0), yaw 0, tip at the rest pose (0, CastTipReach). Returns tip -> landing (what the owner feeds FightSim).
        static float TipToBobberFor(float distance, out float rootToLanding)
        {
            CastMath.Landing(0f, 0f, 0f, distance, out float lx, out float lz);
            Assert.That(lx, Is.EqualTo(0f).Within(Eps));
            rootToLanding = lz;
            float dz = lz - FishingConstants.CastTipReach;
            return dz < 0f ? -dz : dz;
        }

        static RodFrame Fr(bool pp = false, bool ph = false, bool pr = false, bool sp = false, bool sh = true,
                           float p2b = 0f, float t2b = 10f)
        {
            return new RodFrame
            {
                PrimaryPressed = pp, PrimaryHeld = ph, PrimaryReleased = pr,
                SecondaryPressed = sp, SecondaryHeld = sh,
                MarkerValid = true, PlayerToBobber = p2b, TipToBobber = t2b,
            };
        }

        static FishingRoll Roll()
        {
            return new FishingRoll
            {
                Catch = new CatchResult { IsJunk = false, ItemIndex = 0, WeightG = 2000, LengthMm = 500, ValueCoins = 12 },
                WeightT = 0.5f, BiteWait = 1.2f, NibbleCount = 0, FightSeed = 12345u, SizeClass = 1, HookWindow = 0f,
            };
        }

        // Drives the REAL machine: Ready -> Charging -> release at about `charge` -> Casting, leaving CastDistance set.
        static RodStateMachine CastAtCharge(in FishingTuning t, in RodStats rod, float charge)
        {
            var m = new RodStateMachine(in t);
            m.SetRod(in rod);
            m.Tick(0.1f, Fr(sp: true));
            for (int i = 0; i < 3; i++) m.Tick(0.1f, Fr());
            Assert.That(m.State, Is.EqualTo(RodState.Ready));
            m.Tick(0.02f, Fr(pp: true, ph: true));
            Assert.That(m.State, Is.EqualTo(RodState.Charging));
            float held = charge * t.CastChargeTime;
            m.Tick(held, Fr(ph: true));
            m.Tick(0.02f, Fr(pr: true));   // M-4: the release frame does not advance the charge
            Assert.That(m.State, Is.EqualTo(RodState.Casting), "the release at charge " + charge + " must cast (ChargeNorm " + m.ChargeNorm + ")");
            return m;
        }

        // Catches (R67): a minimum-charge cast whose owner start line (tip -> bobber) is <= landDistance, which lands on the first reel tick
        // (the measured bug), for EVERY shipped rod; the tip reach missing from the landing; castMinDist tuned below landDistance.
        [Test]
        public void MinChargeCast_EveryShippedRod_StartsAboveLandDistanceAndDoesNotLandOnTheFirstTick()
        {
            Loaded d = LoadShipped();
            FishFightProfile profile = d.Profiles[0];
            for (int i = 0; i < d.Rods.Count; i++)
            {
                RodStats rod = d.Rods[i];
                FishingTuning t = d.T;
                RodStateMachine m = CastAtCharge(in t, in rod, 0.12f);   // just above MinChargeNorm: the smallest cast the machine allows
                float tipToBobber = TipToBobberFor(m.CastDistance, out float rootToLanding);
                Assert.That(rootToLanding, Is.EqualTo(FishingConstants.CastTipReach + m.CastDistance).Within(Eps), d.RodIds[i] + ": landing from the root");
                Assert.That(tipToBobber, Is.EqualTo(m.CastDistance).Within(Eps), d.RodIds[i] + ": tip -> landing is the cast distance");
                Assert.That(tipToBobber, Is.GreaterThan(t.LandDistance + 0.25f), d.RodIds[i] + ": a min cast must leave real line to reel");

                FishingRoll roll = Roll();
                m.OnCastResult(m.CastId, true, in roll, in profile);
                m.Tick(0.8f, Fr(p2b: rootToLanding));
                Assert.That(m.State, Is.EqualTo(RodState.Waiting), d.RodIds[i]);
                for (int k = 0; k < 6 && m.State == RodState.Waiting; k++) m.Tick(0.5f, Fr(p2b: rootToLanding));
                Assert.That(m.State, Is.EqualTo(RodState.Bite), d.RodIds[i]);
                m.Tick(0.3f, Fr(p2b: rootToLanding, t2b: tipToBobber));
                Assert.That(m.State, Is.EqualTo(RodState.Hooked), d.RodIds[i]);

                Assert.That(m.Fight.State.LineLength, Is.EqualTo(tipToBobber).Within(Eps), d.RodIds[i] + ": the fight starts with the tip -> bobber line");
                m.TickFight(new FightInput(0f, true), Fr());
                Assert.That(m.State, Is.EqualTo(RodState.Hooked), d.RodIds[i] + ": must NOT land on the first reel tick (line " + tipToBobber.ToString("F3", CultureInfo.InvariantCulture) + ")");
                Assert.That(m.Fight.State.Outcome, Is.EqualTo(FightOutcome.None), d.RodIds[i]);
                Assert.That(m.Fight.State.LineLength, Is.GreaterThan(t.LandDistance), d.RodIds[i] + ": still above landDistance after one tick");
            }
        }

        // Catches (R67 maxLineOut): the max-cast landing (root distance castTipReach + castMaxDist) eating castTipReach of the walk-away slack
        // (the owner's maxLineOut is castMaxDist + spoolExtra measured from the TIP, so the machine subtracts castTipReach from PlayerToBobber):
        // the player may walk exactly spoolExtra further from a max cast, and not more. The machine must keep Waiting at a max cast.
        [Test]
        public void MaxChargeCast_EveryShippedRod_StaysWithinMaxLineOut()
        {
            Loaded d = LoadShipped();
            FishFightProfile profile = d.Profiles[0];
            for (int i = 0; i < d.Rods.Count; i++)
            {
                RodStats rod = d.Rods[i];
                FishingTuning t = d.T;
                RodStateMachine m = CastAtCharge(in t, in rod, 1f);
                float rootToLanding = FishingConstants.CastTipReach + rod.CastMaxDist;
                CastMath.Landing(0f, 0f, 0f, m.CastDistance, out _, out float lz);
                Assert.That(lz, Is.EqualTo(rootToLanding).Within(1e-3f), d.RodIds[i] + ": max charge lands at castTipReach + castMaxDist");
                Assert.That(rootToLanding, Is.LessThan(rod.MaxLineOut(t.SpoolExtra)), d.RodIds[i] + ": the landing is inside maxLineOut");

                FishingRoll roll = Roll();
                roll.BiteWait = 30f;
                m.OnCastResult(m.CastId, true, in roll, in profile);
                m.Tick(1.5f, Fr(p2b: rootToLanding));
                Assert.That(m.State, Is.EqualTo(RodState.Waiting), d.RodIds[i]);
                m.Tick(0.5f, Fr(p2b: rootToLanding));
                Assert.That(m.State, Is.EqualTo(RodState.Waiting), d.RodIds[i] + ": a max cast must not be auto-reeled by maxLineOut");
                m.Tick(0.1f, Fr(p2b: rootToLanding + t.SpoolExtra - 0.05f));
                Assert.That(m.State, Is.EqualTo(RodState.Waiting), d.RodIds[i] + ": the full spoolExtra of slack is left (limit, not beyond it)");
                m.Tick(0.1f, Fr(p2b: rootToLanding + t.SpoolExtra + 0.3f));
                Assert.That(m.State, Is.EqualTo(RodState.Idle), d.RodIds[i] + ": beyond castMaxDist + spoolExtra from the tip auto-reels");
            }
        }

        // The same first-tick rule on the pure test data (spec values), so a change of the shipped assets cannot hide a regression here.
        [Test]
        public void MinChargeCast_TestDataRods_DoNotLandOnTheFirstTick()
        {
            FishingTuning t = FishingTestData.Tuning();
            RodStats[] rods = FishingTestData.Rods();
            FishFightProfile profile = FishingTestData.MotionProfiles()[FishingTestData.ProfileBulldog];
            foreach (RodStats rod in rods)
            {
                float distance = CastMath.Distance(0f, t.CastMinDist, rod.CastMaxDist);
                float tipToBobber = TipToBobberFor(distance, out _);
                var sim = new FightSim(profile, rod, t, 0.5f, 99u, tipToBobber, false, false);
                Assert.That(sim.State.LineLength, Is.EqualTo(t.CastMinDist).Within(Eps), "charge 0 start line is castMinDist");
                Assert.That(sim.Tick(new FightInput(0f, true)), Is.EqualTo(FightOutcome.None), "rod cast " + rod.CastMaxDist + ": no first-tick landing at charge 0");
            }
        }

        // Report only: per shipped rod at charge 0 (the pure minimum, not castable: below MinChargeNorm), 10% and 25%: the owner start
        // line, and over all shipped fight profiles x 20 seeds with an always-reel bot (Steer 0, Reel true) the time to Landed and whether the
        // host's clean-claim checks (including the landed floor) would accept it. Asserts only that every start line clears landDistance.
        [Test]
        public void MinChargeCast_Measurement_PrintsLineStartAndTimeToLandPerRod()
        {
            Loaded d = LoadShipped();
            FishingTuning t = d.T;
            float[] charges = { 0f, 0.10f, 0.25f };
            for (int i = 0; i < d.Rods.Count; i++)
            {
                RodStats rod = d.Rods[i];
                foreach (float charge in charges)
                {
                    float distance = CastMath.Distance(charge, t.CastMinDist, rod.CastMaxDist);
                    float tipToBobber = TipToBobberFor(distance, out float rootToLanding);
                    float hostLine0 = CatchValidator.HostLineLength0(rootToLanding, t.LandDistance);
                    hostLine0 = CatchValidator.WalkBoundedLineLength0(hostLine0, rootToLanding, CastMath.FlightTime(distance) + 4.0, t.LandDistance);
                    float floor = CatchValidator.LandedFloorSeconds(hostLine0, t.LandDistance, rod.ReelSpeed);
                    Assert.That(tipToBobber, Is.GreaterThan(t.LandDistance), d.RodIds[i] + " charge " + charge);

                    var times = new List<float>();
                    int fights = 0, landed = 0, clean = 0;
                    for (int p = 0; p < d.Profiles.Count; p++)
                    {
                        for (int s = 0; s < 20; s++)
                        {
                            FishFightProfile profile = d.Profiles[p];
                            var sim = new FightSim(profile, rod, t, 0.5f, unchecked(2654435761u * (uint)(s + 1)), tipToBobber, false, false);
                            int maxTicks = (int)Math.Ceiling(t.MaxFightTime / FightSim.Step);
                            for (int k = 0; k < maxTicks; k++)
                            {
                                if (sim.Tick(new FightInput(0f, true)) != FightOutcome.None) break;
                            }
                            fights++;
                            FightState f = sim.State;
                            if (f.Outcome != FightOutcome.Landed) continue;
                            landed++;
                            times.Add(f.Elapsed);
                            var report = new FightReport { MaxTension = f.MaxTension, FightSeconds = f.Elapsed, NearSnapRecoveries = f.NearSnapRecoveries };
                            var cast = new ActiveCast { Roll = Roll(), LineLength0 = hostLine0, THooked = 0.0 };
                            if (CatchValidator.CleanClaimAccepted(in report, f.Elapsed, in cast, t.LandDistance, rod.ReelSpeed, t.CleanCatchMaxTension)) clean++;
                        }
                    }

                    times.Sort();
                    string median = times.Count > 0 ? times[times.Count / 2].ToString("F2", CultureInfo.InvariantCulture) : "n/a";
                    string min = times.Count > 0 ? times[0].ToString("F2", CultureInfo.InvariantCulture) : "n/a";
                    NUnit.Framework.TestContext.WriteLine(string.Format(CultureInfo.InvariantCulture,
                        "FG R67 {0} charge={1:F2} castDist={2:F2} rootToLanding={3:F2} ownerStartLine(TipToBobber)={4:F2} hostLineLength0={5:F2} landedFloor={6:F2}s reel={7:F1} | always-reel x{8}: landed={9} medianT={10}s minT={11}s cleanClaimAccepted={12}",
                        d.RodIds[i], charge, distance, rootToLanding, tipToBobber, hostLine0, floor, rod.ReelSpeed, fights, landed, median, min, clean));
                }
            }
        }
    }
}
