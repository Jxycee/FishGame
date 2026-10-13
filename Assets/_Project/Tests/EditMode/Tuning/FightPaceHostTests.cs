// User playtest 2026-10-13 (F10): fights got shorter, so the host's Landed floor (CatchValidator.LandedFloorSeconds with MaxReelSpeed) must still
// accept every honest catch. Real assets (registry), the R72 bots (FishingBots.RunFight, internal) on every rod and profile; no seeds.
using System;
using System.Collections.Generic;
using System.Globalization;
using System.Linq;
using FishGame.Core;
using FishGame.Data;
using FishGame.EditorTools;
using NUnit.Framework;
using UnityEditor;

namespace FishGame.Tests.EditMode
{
    [Category("Tuning")]
    public class FightPaceHostTests
    {
        const int Fights = 200;

        static ContentRegistry LoadRegistry()
        {
            var registry = AssetDatabase.LoadAssetAtPath<ContentRegistry>(AssetPaths.ContentRegistryAsset);
            Assert.That(registry, Is.Not.Null, "missing " + AssetPaths.ContentRegistryAsset + ": run Tools/unity.ps1 build first");
            return registry;
        }

        // Catches: a retune that lets an honest, bot-driven landing finish before the host's floor (the host would refuse it as LandedTooFast and
        // the player would lose the fish): any rod x profile x bot x seed. The owner's start line is TipToBobber; the host's is hostDistance - 3.1,
        // which is 0.7 m shorter than the owner's (3.1 slack - 2.4 tip reach), so the floor is computed from the SHORTER host line (the lenient side
        // for the player is the longer line, so this is the strict check). Also checks MaxFightTime / FightTimeout are not tripped by a landing.
        [Test]
        public void FightPace_HonestBotLandings_NeverTripTheHostLandedFloorOrCaps()
        {
            ContentRegistry registry = LoadRegistry();
            FishingTuning tuning = registry.FishingTuning.ToCore();
            RodDef[] rods = registry.Rods.ToArray();
            Assert.That(rods.Length, Is.EqualTo(4));
            float hostSlack = FishingConstants.HookedLineSlack - FishingConstants.CastTipReach;
            double worstMargin = double.PositiveInfinity;
            string worstWhere = "";
            int landings = 0;
            double longest = 0;

            for (int p = 0; p < registry.FightProfiles.Count; p++)
            {
                FishFightProfile profile = registry.FightProfileAt((ushort)p).ToCore();
                foreach (RodDef rodDef in rods)
                {
                    RodStats rod = rodDef.ToCore();
                    float speed = CatchValidator.MaxReelSpeed(in rod, in profile, in tuning);
                    foreach (FishingBots.BotKind bot in new[] { FishingBots.BotKind.Rhythm, FishingBots.BotKind.Reckless, FishingBots.BotKind.Pump, FishingBots.BotKind.Greedy })
                        for (int i = 0; i < Fights; i++)
                        {
                            FishingBots.FightRecord rec = FishingBots.RunFight(bot, in profile, in rod, in tuning, i);
                            if (rec.Outcome != FightOutcome.Landed) continue;
                            FishingBots.CastFor(i, out float ownerLine, out _);
                            float hostLine = CatchValidator.HostLineLength0(ownerLine - hostSlack + FishingConstants.HookedLineSlack, tuning.LandDistance);
                            float floor = CatchValidator.LandedFloorSeconds(hostLine, tuning.LandDistance, speed);
                            double margin = rec.Elapsed - floor;
                            landings++;
                            if (rec.Elapsed > longest) longest = rec.Elapsed;
                            if (margin < worstMargin)
                            {
                                worstMargin = margin;
                                worstWhere = registry.FightProfileAt((ushort)p).Id + " " + rodDef.Id + " " + bot + " seed " + i + " elapsed " + rec.Elapsed.ToString("F2", CultureInfo.InvariantCulture)
                                             + " floor " + floor.ToString("F2", CultureInfo.InvariantCulture);
                            }
                            Assert.That(rec.Elapsed, Is.GreaterThanOrEqualTo(floor), "LandedTooFast: " + registry.FightProfileAt((ushort)p).Id + " " + rodDef.Id + " " + bot + " seed " + i);
                            Assert.That(rec.Elapsed, Is.LessThan(tuning.FightTimeout), "a landing may not outlast the fight timeout");
                        }
                }
            }

            TestContext.WriteLine("FG PACE landings=" + landings + " worst margin over the host floor=" + worstMargin.ToString("F2", CultureInfo.InvariantCulture) + "s at " + worstWhere
                                  + " longest landing=" + longest.ToString("F1", CultureInfo.InvariantCulture) + "s (MaxFightTime " + tuning.MaxFightTime.ToString("F0", CultureInfo.InvariantCulture) + ")");
            Assert.That(landings, Is.GreaterThan(1000), "the check must actually see landings");
            Assert.That(longest, Is.LessThan(tuning.MaxFightTime));
        }

        // Catches: the surge or any reel multiplier outrunning the host's bound on a REAL (shipped) tuning: the fastest honest reel the sim can
        // produce in one tick, over a grid of fight ages and tire values, never beats MaxReelSpeed (the speed the host's floor divides by).
        [Test]
        public void FightPace_ShippedTuning_ReelNeverBeatsTheHostMaxReelSpeed()
        {
            ContentRegistry registry = LoadRegistry();
            FishingTuning tuning = registry.FishingTuning.ToCore();
            Assert.That(tuning.ReelOpenMult, Is.LessThanOrEqualTo(tuning.TireReelMaxMult), "the surge is a floor under the tire curve, capped at its max");
            for (int p = 0; p < registry.FightProfiles.Count; p++)
            {
                FishFightProfile profile = registry.FightProfileAt((ushort)p).ToCore();
                foreach (RodDef rodDef in registry.Rods)
                {
                    RodStats rod = rodDef.ToCore();
                    float cap = CatchValidator.MaxReelSpeed(in rod, in profile, in tuning) * FightSim.Step;
                    foreach (float elapsed in new[] { 0f, 0.1f, 0.4f, 0.79f, 1.5f, 20f })
                        foreach (float tire in new[] { 0f, 1f, 3f, tuning.TireReelFull, 2f * tuning.TireReelFull, tuning.TireRunFull })
                        {
                            var sim = new FightSim(in profile, in rod, in tuning, 0.5f, 1u, 12f, false, false);
                            sim.ForceState(new FightState { LineLength = 12f, Phase = FightPhase.Calm, PhaseTimer = 100f, Elapsed = elapsed, Tire = tire });
                            sim.Tick(new FightInput(0f, true));
                            double moved = 12.0 - sim.State.LineLength;
                            Assert.That(moved, Is.LessThanOrEqualTo(cap + 1e-5), registry.FightProfileAt((ushort)p).Id + " " + rodDef.Id + " elapsed " + elapsed + " tire " + tire);
                        }
                }
            }
        }
    }
}
