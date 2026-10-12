using System.Collections.Generic;
using FishGame.Core;
using FishGame.EditorTools;
using FishGame.Fishing;
using NUnit.Framework;
using Unity.Collections.LowLevel.Unsafe;
using UnityEngine;

namespace FishGame.Tests.EditMode
{
    // T17 (C 10.3): the unmanaged wire structs of FishingRodNetwork and the pure seams around them. The NetworkBehaviours themselves are
    // proven by T19's PlayMode tests (C 2.8 #8).
    public class FishingWireTests
    {
        static RodStateData BaseState()
        {
            return new RodStateData { State = 5, Flags = 3, LateralQ = -40, TensionQ = 200, BobberPos = new Vector3(1.5f, 0f, -8.25f), CastId = 77 };
        }

        // Catches: an Equals that skips a field (NetworkVariable would then never send that change, N3) or a GetHashCode that disagrees with it.
        [Test]
        public void RodStateData_Equality_FieldWise()
        {
            RodStateData a = BaseState();
            RodStateData b = BaseState();
            Assert.That(a.Equals(b), Is.True, "identical values");
            Assert.That(a.GetHashCode(), Is.EqualTo(b.GetHashCode()), "identical hash");

            RodStateData m = b; m.State = 6;
            Assert.That(a.Equals(m), Is.False, "State");
            m = b; m.Flags = 4;
            Assert.That(a.Equals(m), Is.False, "Flags");
            m = b; m.LateralQ = -39;
            Assert.That(a.Equals(m), Is.False, "LateralQ");
            m = b; m.TensionQ = 201;
            Assert.That(a.Equals(m), Is.False, "TensionQ");
            m = b; m.BobberPos.x += 0.001f;
            Assert.That(a.Equals(m), Is.False, "BobberPos.x");
            m = b; m.BobberPos.y += 0.001f;
            Assert.That(a.Equals(m), Is.False, "BobberPos.y");
            m = b; m.BobberPos.z += 0.001f;
            Assert.That(a.Equals(m), Is.False, "BobberPos.z");
            m = b; m.CastId = 78;
            Assert.That(a.Equals(m), Is.False, "CastId");
        }

        // Catches: a field reorder or a widened type that bloats the per-tick NetworkVariable (spec 9.9 budget).
        [Test]
        public void RodStateData_Size_AtMostTwentyFourBytes()
        {
            int size = UnsafeUtility.SizeOf<RodStateData>();
            TestContext.WriteLine("FG WIRE RodStateData size=" + size + " bytes");
            Assert.That(size, Is.LessThanOrEqualTo(24));
        }

        static CatchResult SampleCatch()
        {
            return new CatchResult { IsJunk = false, ItemIndex = 3, WeightG = 1234, LengthMm = 321, Quality = CatchQuality.Good, ValueCoins = 56 };
        }

        // Catches: the clean flag read from the wrong bit (or ignoring flags), so a normal catch toasts clean or a clean one does not.
        [Test]
        public void CatchAnnouncement_CleanFlag_SetsCleanBonus()
        {
            CatchResultNet c = CatchResultNet.From(SampleCatch());

            CatchAnnouncement clean = CatchAnnouncement.FromWire(2, in c, (byte)AnnounceKind.Landed, RodWireQuant.AnnounceFlagClean, 5);
            Assert.That(clean.CleanBonus, Is.True);
            Assert.That(clean.Kind, Is.EqualTo(AnnounceKind.Landed));
            Assert.That(clean.CatcherSlot, Is.EqualTo((byte)2));
            Assert.That(clean.CatcherClientId, Is.EqualTo(5UL));
            Assert.That(clean.Catch.WeightG, Is.EqualTo(1234));

            CatchAnnouncement plain = CatchAnnouncement.FromWire(2, in c, (byte)AnnounceKind.Landed, 0, 5);
            Assert.That(plain.CleanBonus, Is.False, "flags 0");

            CatchAnnouncement others = CatchAnnouncement.FromWire(2, in c, (byte)AnnounceKind.Landed, 0xFE, 5);
            Assert.That(others.CleanBonus, Is.False, "flags 0xFE: other bits only");
        }

        static FishingDatabase SeedDb(out int wormIndex)
        {
            FishingDatabase db = FishingSeedData.BuildDatabase();
            var ids = new List<string>();
            foreach (var b in FishingSeedData.Baits) ids.Add(b.id);
            ids.Sort(string.CompareOrdinal);
            wormIndex = ids.IndexOf("bait_worm");
            Assert.That(wormIndex, Is.GreaterThanOrEqualTo(0), "bait_worm in the seed");
            return db;
        }

        static void AssertSameRoll(in FishingRoll want, in FishingRoll got, string label)
        {
            Assert.That(got.Catch.IsJunk, Is.EqualTo(want.Catch.IsJunk), label + " IsJunk");
            Assert.That(got.Catch.ItemIndex, Is.EqualTo(want.Catch.ItemIndex), label + " ItemIndex");
            Assert.That(got.Catch.WeightG, Is.EqualTo(want.Catch.WeightG), label + " WeightG");
            Assert.That(got.Catch.LengthMm, Is.EqualTo(want.Catch.LengthMm), label + " LengthMm");
            Assert.That(got.Catch.Quality, Is.EqualTo(want.Catch.Quality), label + " Quality");
            Assert.That(got.Catch.ValueCoins, Is.EqualTo(want.Catch.ValueCoins), label + " ValueCoins");
            Assert.That(got.WeightT, Is.EqualTo(want.WeightT), label + " WeightT");
            Assert.That(got.BiteWait, Is.EqualTo(want.BiteWait), label + " BiteWait");
            Assert.That(got.NibbleCount, Is.EqualTo(want.NibbleCount), label + " NibbleCount");
            Assert.That(got.Nibble0, Is.EqualTo(want.Nibble0), label + " Nibble0");
            Assert.That(got.Nibble1, Is.EqualTo(want.Nibble1), label + " Nibble1");
            Assert.That(got.Nibble2, Is.EqualTo(want.Nibble2), label + " Nibble2");
            Assert.That(got.FightSeed, Is.EqualTo(want.FightSeed), label + " FightSeed");
            Assert.That(got.SizeClass, Is.EqualTo(want.SizeClass), label + " SizeClass");
            Assert.That(got.HookWindow, Is.EqualTo(want.HookWindow), label + " HookWindow");
            Assert.That(got.Pity, Is.EqualTo(want.Pity), label + " Pity");
        }

        // Catches: any roll field dropped or mistyped on the wire (a wrong FightSeed desyncs the fight), and the R65 Pity bit lost or stuck.
        [Test]
        public void FishingRollNet_RoundTrip_PreservesRoll()
        {
            FishingDatabase db = SeedDb(out int worm);
            Pcg32 rng = Rng.Stream(42, FishingConstants.CatchStreamId, 1);
            int fish = 0, junk = 0, pity = 0;
            for (int i = 0; i < 50; i++)
            {
                var req = new RollRequest { BaitIndex = worm, LoadedCount = 1, Depth = 3f, ForcedItemIndex = -1 };
                FishingRoll roll = PlaceholderRoller.Roll(db, in req, ref rng);
                roll.Pity = (i % 2) == 1;
                if (roll.Pity) pity++;
                if (roll.Catch.IsJunk) junk++; else fish++;

                FishingRollNet wire = FishingRollNet.From(in roll);
                Assert.That(wire.TryToCore(db, out FishingRoll back), Is.True, "roll " + i + " sane");
                AssertSameRoll(in roll, in back, "roll " + i);
            }
            TestContext.WriteLine("FG WIRE rolls fish=" + fish + " junk=" + junk + " pity=" + pity);
            Assert.That(fish, Is.GreaterThan(0), "the sample must contain fish");
            Assert.That(pity, Is.EqualTo(25));
        }

        // Catches: other flag bits turning Pity on (forward compatibility: unknown bits are ignored) and Pity being set by default.
        [Test]
        public void FishingRollNet_Flags_OnlyBitZeroIsPity()
        {
            FishingDatabase db = SeedDb(out int worm);
            Pcg32 rng = Rng.Stream(42, FishingConstants.CatchStreamId, 2);
            var req = new RollRequest { BaitIndex = worm, LoadedCount = 1, Depth = 3f, ForcedItemIndex = -1 };
            FishingRoll roll = PlaceholderRoller.Roll(db, in req, ref rng);
            roll.Pity = false;

            FishingRollNet wire = FishingRollNet.From(in roll);
            Assert.That(wire.Flags, Is.EqualTo((byte)0), "Pity false: no flag bits");
            wire.Flags = 0xFE;
            Assert.That(wire.TryToCore(db, out FishingRoll back), Is.True);
            Assert.That(back.Pity, Is.False, "bits 1..7 are ignored");

            roll.Pity = true;
            wire = FishingRollNet.From(in roll);
            Assert.That(wire.Flags, Is.EqualTo(FishingRollNet.FlagPity));
            wire.Flags = 0xFF;
            Assert.That(wire.TryToCore(db, out back), Is.True);
            Assert.That(back.Pity, Is.True, "bit 0 set");
        }

        // Catches: TryToCore skipping the sanity check (a hostile or corrupt roll reaching the fight) or throwing on a null database.
        [Test]
        public void FishingRollNet_InsaneRoll_TryToCoreFalse()
        {
            FishingDatabase db = SeedDb(out int worm);
            Pcg32 rng = Rng.Stream(42, FishingConstants.CatchStreamId, 3);
            var req = new RollRequest { BaitIndex = worm, LoadedCount = 1, Depth = 3f, ForcedItemIndex = -1 };
            FishingRoll fishRoll;
            int guard = 0;
            do { fishRoll = PlaceholderRoller.Roll(db, in req, ref rng); } while (fishRoll.Catch.IsJunk && ++guard < 100);
            Assert.That(fishRoll.Catch.IsJunk, Is.False, "need a fish roll");

            FishingRollNet good = FishingRollNet.From(in fishRoll);
            Assert.That(good.TryToCore(db, out _), Is.True, "control");

            FishingRollNet bad = good; bad.FightSeed = 0;
            Assert.That(bad.TryToCore(db, out _), Is.False, "FightSeed 0");
            bad = good; bad.Catch.ItemIndex = 999;
            Assert.That(bad.TryToCore(db, out _), Is.False, "species 999");
            bad = good; bad.BiteWait = float.NaN;
            Assert.That(bad.TryToCore(db, out _), Is.False, "BiteWait NaN");
            bad = good; bad.NibbleCount = 4;
            Assert.That(bad.TryToCore(db, out _), Is.False, "NibbleCount 4");
            Assert.That(good.TryToCore(null, out _), Is.False, "null db");
        }

        // Catches: a swapped or truncated field in the catch mirror (weight int, length ushort, quality byte), fish and junk alike.
        [Test]
        public void CatchResultNet_RoundTrip()
        {
            CatchResult fish = SampleCatch();
            CatchResult back = CatchResultNet.From(fish).ToCore();
            Assert.That(back.IsJunk, Is.False);
            Assert.That(back.ItemIndex, Is.EqualTo(fish.ItemIndex));
            Assert.That(back.WeightG, Is.EqualTo(fish.WeightG));
            Assert.That(back.LengthMm, Is.EqualTo(fish.LengthMm));
            Assert.That(back.Quality, Is.EqualTo(fish.Quality));
            Assert.That(back.ValueCoins, Is.EqualTo(fish.ValueCoins));

            var junk = new CatchResult { IsJunk = true, ItemIndex = 1, WeightG = 0, LengthMm = 0, Quality = CatchQuality.Normal, ValueCoins = 2 };
            back = CatchResultNet.From(junk).ToCore();
            Assert.That(back.IsJunk, Is.True);
            Assert.That(back.ItemIndex, Is.EqualTo(junk.ItemIndex));
            Assert.That(back.WeightG, Is.EqualTo(0));
            Assert.That(back.Quality, Is.EqualTo(CatchQuality.Normal));
            Assert.That(back.ValueCoins, Is.EqualTo(2));

            var trophy = new CatchResult { IsJunk = false, ItemIndex = 65535, WeightG = int.MaxValue, LengthMm = 65535, Quality = CatchQuality.Trophy, ValueCoins = int.MaxValue };
            back = CatchResultNet.From(trophy).ToCore();
            Assert.That(back.ItemIndex, Is.EqualTo(trophy.ItemIndex));
            Assert.That(back.WeightG, Is.EqualTo(trophy.WeightG));
            Assert.That(back.LengthMm, Is.EqualTo(trophy.LengthMm));
            Assert.That(back.Quality, Is.EqualTo(CatchQuality.Trophy));
            Assert.That(back.ValueCoins, Is.EqualTo(trophy.ValueCoins));
        }

        // Catches: a wrong remote transition cue (Recall vs Landing mixed up, Charging -> Ready not an InvalidRelease, a recall-flagged Escaped
        // silent) or a cue raised for transitions the plan leaves to RodEventRpc (Bite, Hooked, Snapped, plain Escaped).
        [Test]
        public void FishingRodNetwork_StateCue_MapsRemoteTransitions()
        {
            Assert.That(FishingRodNetwork.StateCue(RodState.Readying, RodState.Ready, 0), Is.EqualTo(RodCue.Ready));
            Assert.That(FishingRodNetwork.StateCue(RodState.Ready, RodState.Charging, 0), Is.EqualTo(RodCue.ChargeStart));
            Assert.That(FishingRodNetwork.StateCue(RodState.Charging, RodState.Ready, 0), Is.EqualTo(RodCue.InvalidRelease));
            Assert.That(FishingRodNetwork.StateCue(RodState.Charging, RodState.Casting, 0), Is.EqualTo(RodCue.CastRelease));
            Assert.That(FishingRodNetwork.StateCue(RodState.Casting, RodState.Waiting, 0), Is.EqualTo(RodCue.Landing));
            Assert.That(FishingRodNetwork.StateCue(RodState.Casting, RodState.Idle, 0), Is.EqualTo(RodCue.Recall));
            Assert.That(FishingRodNetwork.StateCue(RodState.Waiting, RodState.Idle, 0), Is.EqualTo(RodCue.Recall));
            Assert.That(FishingRodNetwork.StateCue(RodState.Bite, RodState.Idle, 0), Is.EqualTo(RodCue.Recall));
            Assert.That(FishingRodNetwork.StateCue(RodState.Hooked, RodState.Landed, 0), Is.EqualTo(RodCue.LineLanded));
            Assert.That(FishingRodNetwork.StateCue(RodState.Waiting, RodState.Escaped, RodWireQuant.FlagRecall), Is.EqualTo(RodCue.Recall));

            Assert.That(FishingRodNetwork.StateCue(RodState.Waiting, RodState.Bite, 0), Is.EqualTo(RodCue.None), "Bite comes from RodEventRpc");
            Assert.That(FishingRodNetwork.StateCue(RodState.Bite, RodState.Hooked, 0), Is.EqualTo(RodCue.None), "HookSet comes from RodEventRpc");
            Assert.That(FishingRodNetwork.StateCue(RodState.Hooked, RodState.Snapped, 0), Is.EqualTo(RodCue.None), "Snapped comes from RodEventRpc");
            Assert.That(FishingRodNetwork.StateCue(RodState.Hooked, RodState.Escaped, 0), Is.EqualTo(RodCue.None), "Escaped without recall: RodEventRpc");
            Assert.That(FishingRodNetwork.StateCue(RodState.Landed, RodState.Idle, 0), Is.EqualTo(RodCue.None), "no cue returning to Idle after a beat");
        }

        [SetUp]
        public void ResetDebugSettings()
        {
            FishingDebugSettings.ForcedBiteWait = 0f;
            FishingDebugSettings.TwitchyHook = false;
            FishingDebugSettings.ForcedSpeciesId = null;
            FishingDebugSettings.ForceJunk = false;
        }

        // Catches: the override builder starting from default(RollOverrides), whose ForcedItemIndex 0 would force species 0 on every cast (B-Q14).
        [Test]
        public void FishingDebugSettings_ToOverrides_DefaultsEqualNone()
        {
            RollOverrides o = FishingDebugSettings.ToOverrides(null);
            Assert.That(o.ForcedItemIndex, Is.EqualTo(-1), "no forced species");
            Assert.That(o.ForcedBiteWait, Is.EqualTo(0f));
            Assert.That(o.ForceJunk, Is.False);
            Assert.That(o.Twitchy, Is.False);
        }

        // Catches: debug toggles not reaching the roll request, and a negative forced bite wait leaking through (the ledger treats > 0 as forced).
        [Test]
        public void FishingDebugSettings_ToOverrides_CarriesFlagsAndClampsBiteWait()
        {
            FishingDebugSettings.ForcedBiteWait = 0.1f;
            FishingDebugSettings.TwitchyHook = true;
            FishingDebugSettings.ForceJunk = true;
            RollOverrides o = FishingDebugSettings.ToOverrides(null);
            Assert.That(o.ForcedBiteWait, Is.EqualTo(0.1f));
            Assert.That(o.Twitchy, Is.True);
            Assert.That(o.ForceJunk, Is.True);
            Assert.That(o.ForcedItemIndex, Is.EqualTo(-1));

            FishingDebugSettings.ForcedBiteWait = -3f;
            Assert.That(FishingDebugSettings.ToOverrides(null).ForcedBiteWait, Is.EqualTo(0f), "negative clamps to 0");
        }
    }
}
