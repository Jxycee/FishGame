using System.Collections.Generic;
using FishGame.Core;
using NUnit.Framework;

namespace FishGame.Tests.EditMode
{
    // Spec 9.2 rows (C 10.2): one test per row, plus the rule tests. Plan T8 holds the arrange/act/assert table.
    // Drive helpers chain: ToBite calls ToWaiting if the machine has not got there yet, and so on. They drain the
    // effect queue before returning, so a test only sees what happens after the helper.
    public class RodStateMachineTests
    {
        const float Eps = 1e-5f;

        static FishingTuning Tun() => FishingTestData.Tuning();
        static RodStats Handmade() => FishingTestData.Rods()[FishingTestData.RodHandmade];
        static FishFightProfile Carp() => FishingTestData.MotionProfiles()[FishingTestData.ProfileBulldog];
        static FightInput In(float steer, bool reel) => new FightInput(steer, reel);

        static RodStateMachine New()
        {
            FishingTuning t = Tun();
            var m = new RodStateMachine(in t);
            RodStats rod = Handmade();
            m.SetRod(in rod);
            return m;
        }

        static RodFrame Fr(bool pp = false, bool ph = false, bool pr = false, bool sp = false, bool sh = true, bool sr = false,
                           bool marker = true, float p2b = 0f, float t2b = 10f, float disp = 0f, bool water = false, bool paused = false)
        {
            return new RodFrame
            {
                PrimaryPressed = pp, PrimaryHeld = ph, PrimaryReleased = pr,
                SecondaryPressed = sp, SecondaryHeld = sh, SecondaryReleased = sr,
                MarkerValid = marker, PlayerToBobber = p2b, TipToBobber = t2b, HookDisplacement = disp,
                InWater = water, Paused = paused
            };
        }

        static List<RodEffect> Drain(RodStateMachine m)
        {
            var l = new List<RodEffect>();
            m.DrainEffects(l);
            return l;
        }

        static int Count(List<RodEffect> e, RodEffectKind k)
        {
            int n = 0;
            for (int i = 0; i < e.Count; i++) if (e[i].Kind == k) n++;
            return n;
        }

        static bool HasCue(List<RodEffect> e, RodCue c)
        {
            for (int i = 0; i < e.Count; i++) if (e[i].Kind == RodEffectKind.Cue && e[i].Cue == c) return true;
            return false;
        }

        static int CueCount(List<RodEffect> e, RodCue c)
        {
            int n = 0;
            for (int i = 0; i < e.Count; i++) if (e[i].Kind == RodEffectKind.Cue && e[i].Cue == c) n++;
            return n;
        }

        static RodEffect First(List<RodEffect> e, RodEffectKind k)
        {
            for (int i = 0; i < e.Count; i++) if (e[i].Kind == k) return e[i];
            Assert.Fail("no effect of kind " + k);
            return default;
        }

        static RodEffect RodEv(List<RodEffect> e, RodEventType t)
        {
            for (int i = 0; i < e.Count; i++) if (e[i].Kind == RodEffectKind.SendRodEvent && e[i].EventType == t) return e[i];
            Assert.Fail("no SendRodEvent of type " + t);
            return default;
        }

        static FishingRoll FishRoll(float biteWait = 2f, int nibbles = 0, float hookWindow = 0f, bool junk = false,
                                    float n0 = 0f, float n1 = 0f, bool pity = false)
        {
            return new FishingRoll
            {
                Catch = new CatchResult { IsJunk = junk, ItemIndex = (ushort)FishingTestData.SpeciesCarp, WeightG = junk ? 0 : 2000, LengthMm = junk ? (ushort)0 : (ushort)500, ValueCoins = 12 },
                WeightT = 0.5f, BiteWait = biteWait, NibbleCount = (byte)nibbles, Nibble0 = n0, Nibble1 = n1,
                FightSeed = 12345u, SizeClass = 1, HookWindow = hookWindow, Pity = pity
            };
        }

        // ---- drive helpers ----------------------------------------------------------------------------------

        static void ToReady(RodStateMachine m)
        {
            m.Tick(0.1f, Fr(sp: true));
            for (int i = 0; i < 3; i++) m.Tick(0.1f, Fr());
            Assert.That(m.State, Is.EqualTo(RodState.Ready));
            Drain(m);
        }

        static void ToCharging(RodStateMachine m)
        {
            if (m.State == RodState.Idle) ToReady(m);
            m.Tick(0.1f, Fr(pp: true, ph: true));
            Assert.That(m.State, Is.EqualTo(RodState.Charging));
            Drain(m);
        }

        // Charge 0.5: distance 10.25, flight 0.573 s, CastId 1.
        static void ToCasting(RodStateMachine m)
        {
            if (m.State < RodState.Charging) ToCharging(m);
            m.Tick(0.4f, Fr(ph: true));
            m.Tick(0.1f, Fr(pr: true));
            Assert.That(m.State, Is.EqualTo(RodState.Casting));
            Drain(m);
        }

        static void ToWaiting(RodStateMachine m, FishingRoll? roll = null)
        {
            if (m.State < RodState.Casting) ToCasting(m);
            FishingRoll r = roll ?? FishRoll();
            FishFightProfile p = Carp();
            m.OnCastResult(m.CastId, true, in r, in p);
            m.Tick(0.6f, Fr());
            Assert.That(m.State, Is.EqualTo(RodState.Waiting));
            Drain(m);
        }

        static void ToBite(RodStateMachine m, FishingRoll? roll = null)
        {
            if (m.State < RodState.Waiting) ToWaiting(m, roll);
            for (int i = 0; i < 8 && m.State == RodState.Waiting; i++) m.Tick(0.5f, Fr());
            Assert.That(m.State, Is.EqualTo(RodState.Bite));
            Drain(m);
        }

        static void ToHooked(RodStateMachine m, FishingRoll? roll = null, float tip = 10f)
        {
            if (m.State < RodState.Bite) ToBite(m, roll);
            m.Tick(0.3f, Fr(t2b: tip));
            Assert.That(m.State, Is.EqualTo(RodState.Hooked));
            Drain(m);
        }

        static void ToLanded(RodStateMachine m)
        {
            ToHooked(m, null, 2.05f);
            for (int i = 0; i < 3; i++) m.TickFight(In(0f, true), Fr());
            Assert.That(m.State, Is.EqualTo(RodState.Landed));
            Drain(m);
        }

        static void ForceSnapState(RodStateMachine m, float snapTimer)
        {
            var s = new FightState
            {
                LineLength = 10f, Lateral = 0.7f, Tension = 1f, FishStamina = 1f,
                Phase = FightPhase.Burst, BurstDir = 1, PhaseTimer = 5f, SnapTimer = snapTimer
            };
            m.Fight.ForceState(in s);
        }

        static void ToSnapped(RodStateMachine m)
        {
            ToHooked(m);
            ForceSnapState(m, 0.24f);
            m.TickFight(In(0f, true), Fr());
            Assert.That(m.State, Is.EqualTo(RodState.Snapped));
            Drain(m);
        }

        static void ToEscaped(RodStateMachine m)
        {
            ToBite(m, FishRoll(hookWindow: 0.5f));
            m.Tick(0.6f, Fr());
            Assert.That(m.State, Is.EqualTo(RodState.Escaped));
            Drain(m);
        }

        // ---- C 10.2 rows ------------------------------------------------------------------------------------

        [Test]
        public void RodStateMachine_Idle_SecondaryPressed_EntersReadying()
        {
            RodStateMachine m = New();
            m.Tick(0.1f, Fr(sh: true));   // held without a fresh press edge
            Assert.That(m.State, Is.EqualTo(RodState.Idle));
            m.Tick(0.1f, Fr(sp: true));
            Assert.That(m.State, Is.EqualTo(RodState.Readying));
            Assert.That(Drain(m), Is.Empty);
        }

        [Test]
        public void RodStateMachine_Readying_SecondaryHeldQuarterSecond_EntersReadyWithMarker()
        {
            RodStateMachine m = New();
            m.Tick(0.1f, Fr(sp: true));
            m.Tick(0.1f, Fr());
            m.Tick(0.1f, Fr());
            Assert.That(m.State, Is.EqualTo(RodState.Readying), "at 0.2 s");
            Assert.That(m.MarkerOn, Is.False);
            Drain(m);
            m.Tick(0.1f, Fr());
            Assert.That(m.State, Is.EqualTo(RodState.Ready), "at 0.3 s");
            Assert.That(m.MarkerOn, Is.True);
            Assert.That(HasCue(Drain(m), RodCue.Ready), Is.True);
        }

        [Test]
        public void RodStateMachine_Readying_SecondaryReleased_ReturnsIdle()
        {
            RodStateMachine m = New();
            m.Tick(0.1f, Fr(sp: true));
            m.Tick(0.1f, Fr(sh: false));
            Assert.That(m.State, Is.EqualTo(RodState.Idle));
            Assert.That(Drain(m), Is.Empty);
        }

        [Test]
        public void RodStateMachine_Ready_SecondaryReleased_ReturnsIdleMarkerOff()
        {
            RodStateMachine m = New();
            ToReady(m);
            m.Tick(0.1f, Fr(sh: false));
            Assert.That(m.State, Is.EqualTo(RodState.Idle));
            Assert.That(m.MarkerOn, Is.False);
        }

        [Test]
        public void RodStateMachine_Ready_FreshPrimaryPress_EntersChargingAtZero()
        {
            RodStateMachine m = New();
            ToReady(m);
            m.Tick(0.1f, Fr(pp: true, ph: true));
            Assert.That(m.State, Is.EqualTo(RodState.Charging));
            Assert.That(m.ChargeNorm, Is.EqualTo(0f));
            Assert.That(HasCue(Drain(m), RodCue.ChargeStart), Is.True);
        }

        [Test]
        public void RodStateMachine_Charging_SecondaryReleased_ReturnsIdleWithoutCast()
        {
            RodStateMachine m = New();
            ToCharging(m);
            m.Tick(0.3f, Fr(ph: true, sh: false));
            Assert.That(m.State, Is.EqualTo(RodState.Idle));
            Assert.That(m.CastId, Is.EqualTo(0u));
            Assert.That(Count(Drain(m), RodEffectKind.SendCastRequest), Is.EqualTo(0));
        }

        [Test]
        public void RodStateMachine_Charging_ValidRelease_EntersCastingAndRequestsCast()
        {
            RodStateMachine m = New();
            ToCharging(m);
            m.Tick(0.5f, Fr(ph: true));
            m.Tick(0.0001f, Fr(pr: true));
            Assert.That(m.State, Is.EqualTo(RodState.Casting));
            Assert.That(m.CastId, Is.EqualTo(1u));
            Assert.That(m.CastDistance, Is.EqualTo(10.25f).Within(0.01f));
            List<RodEffect> e = Drain(m);
            Assert.That(HasCue(e, RodCue.CastRelease), Is.True);
            RodEffect req = First(e, RodEffectKind.SendCastRequest);
            Assert.That(req.CastId, Is.EqualTo(1u));
            Assert.That(req.ChargeNorm, Is.EqualTo(0.5f).Within(0.01f));
        }

        [Test]
        public void RodStateMachine_Charging_ShortRelease_ThunksAndStaysReady()
        {
            RodStateMachine m = New();
            ToCharging(m);
            m.Tick(0.05f, Fr(pr: true));
            Assert.That(m.State, Is.EqualTo(RodState.Ready));
            Assert.That(m.CastId, Is.EqualTo(0u));
            List<RodEffect> e = Drain(m);
            Assert.That(HasCue(e, RodCue.InvalidRelease), Is.True);
            Assert.That(Count(e, RodEffectKind.SendCastRequest), Is.EqualTo(0));
        }

        [Test]
        public void RodStateMachine_Charging_InvalidMarkerRelease_ThunksAndStaysReady()
        {
            RodStateMachine m = New();
            ToCharging(m);
            m.Tick(0.5f, Fr(ph: true));
            m.Tick(0.1f, Fr(pr: true, marker: false));
            Assert.That(m.State, Is.EqualTo(RodState.Ready));
            Assert.That(m.CastId, Is.EqualTo(0u));
            List<RodEffect> e = Drain(m);
            Assert.That(HasCue(e, RodCue.InvalidRelease), Is.True);
            Assert.That(Count(e, RodEffectKind.SendCastRequest), Is.EqualTo(0));
        }

        [Test]
        public void RodStateMachine_Casting_FlightEndedWithResult_EntersWaiting()
        {
            RodStateMachine m = New();
            ToCasting(m);
            FishingRoll r = FishRoll();
            FishFightProfile p = Carp();
            m.OnCastResult(1u, true, in r, in p);
            Assert.That(m.State, Is.EqualTo(RodState.Casting), "result held, flight not over");
            Assert.That(m.HasResult, Is.True);
            // flight time for the 10.25 m cast is 0.45 + 0.012 * 10.25 = 0.573 s (CastMath.FlightTime)
            Assert.That(CastMath.FlightTime(m.CastDistance), Is.EqualTo(0.573f).Within(0.001f));
            m.Tick(0.5f, Fr());
            Assert.That(m.State, Is.EqualTo(RodState.Casting), "0.5 s: still in flight");
            Assert.That(HasCue(Drain(m), RodCue.Landing), Is.False, "no Landing cue before the flight ends");
            m.Tick(0.1f, Fr());
            Assert.That(m.State, Is.EqualTo(RodState.Waiting), "0.6 s: flight over");
            Assert.That(m.BiteTimer, Is.EqualTo(0f));
            Assert.That(HasCue(Drain(m), RodCue.Landing), Is.True);
        }

        [Test]
        public void RodStateMachine_Casting_FlightEndedWithoutResult_StaysCastingTimerStopped()
        {
            RodStateMachine m = New();
            ToCasting(m);
            m.Tick(0.6f, Fr());
            m.Tick(1.0f, Fr());
            Assert.That(m.State, Is.EqualTo(RodState.Casting));
            Assert.That(CueCount(Drain(m), RodCue.Landing), Is.EqualTo(1));
            Assert.That(m.BiteTimer, Is.EqualTo(0f));
            Assert.That(m.HasResult, Is.False);
        }

        [Test]
        public void RodStateMachine_Casting_ResultAfterLanding_BiteTimerStartsAtResult()
        {
            RodStateMachine m = New();
            ToCasting(m);
            m.Tick(0.6f, Fr());
            m.Tick(1.0f, Fr());
            FishingRoll r = FishRoll(biteWait: 2f);
            FishFightProfile p = Carp();
            m.OnCastResult(1u, true, in r, in p);
            Assert.That(m.State, Is.EqualTo(RodState.Waiting), "Waiting at once");
            Assert.That(m.BiteTimer, Is.EqualTo(0f));
            m.Tick(1.9f, Fr());
            Assert.That(m.State, Is.EqualTo(RodState.Waiting));
            m.Tick(0.2f, Fr());
            Assert.That(m.State, Is.EqualTo(RodState.Bite), "2.0 s after the result, not after landing");
        }

        [Test]
        public void RodStateMachine_Casting_InvalidResult_RecallsToIdle()
        {
            RodStateMachine m = New();
            ToCasting(m);
            m.OnCastResult(1u, false, default(FishingRoll), default(FishFightProfile));
            Assert.That(m.State, Is.EqualTo(RodState.Idle));
            List<RodEffect> e = Drain(m);
            Assert.That(HasCue(e, RodCue.Recall), Is.True);
            Assert.That(Count(e, RodEffectKind.SendCancel), Is.EqualTo(0));
        }

        [Test]
        public void RodStateMachine_Waiting_InvalidResult_RecallsToIdle()
        {
            RodStateMachine m = New();
            ToWaiting(m);
            m.OnCastResult(1u, false, default(FishingRoll), default(FishFightProfile));
            Assert.That(m.State, Is.EqualTo(RodState.Idle));
            List<RodEffect> e = Drain(m);
            Assert.That(HasCue(e, RodCue.Recall), Is.True);
            Assert.That(Count(e, RodEffectKind.SendCancel), Is.EqualTo(0));
        }

        [Test]
        public void RodStateMachine_Casting_ResultTimeout_RecallsAndSendsCancel()
        {
            RodStateMachine m = New();
            ToCasting(m);
            m.Tick(1.0f, Fr());
            m.Tick(1.0f, Fr());
            Assert.That(m.State, Is.EqualTo(RodState.Casting), "at 2.0 s");
            Drain(m);
            m.Tick(1.0f, Fr());
            Assert.That(m.State, Is.EqualTo(RodState.Idle), "at 3.0 s");
            List<RodEffect> e = Drain(m);
            Assert.That(First(e, RodEffectKind.SendCancel).CastId, Is.EqualTo(1u));
            Assert.That(HasCue(e, RodCue.Recall), Is.True);
            FishingRoll r = FishRoll();
            FishFightProfile p = Carp();
            m.OnCastResult(1u, true, in r, in p);                     // late reply to our own cancel
            m.OnCommit(1u, true, FightOutcome.Cancelled, EscapeReason.None);
            Assert.That(m.IgnoredEventCount, Is.EqualTo(0));
            Assert.That(m.State, Is.EqualTo(RodState.Idle));
        }

        [Test]
        public void RodStateMachine_Waiting_SecondaryPressed_CancelsToIdle()
        {
            RodStateMachine m = New();
            ToWaiting(m);
            m.Tick(0.1f, Fr(sp: true));
            Assert.That(m.State, Is.EqualTo(RodState.Idle));
            List<RodEffect> e = Drain(m);
            Assert.That(Count(e, RodEffectKind.SendCancel), Is.EqualTo(1));
            Assert.That(HasCue(e, RodCue.Recall), Is.True);
            m.OnCommit(1u, true, FightOutcome.Cancelled, EscapeReason.None);
            Assert.That(m.IgnoredEventCount, Is.EqualTo(0));
            Assert.That(m.State, Is.EqualTo(RodState.Idle));
        }

        [Test]
        public void RodStateMachine_Waiting_BeyondMaxLineOut_AutoReelsAndCancels()
        {
            RodStateMachine m = New();
            ToWaiting(m);
            // R67: PlayerToBobber is root -> bobber; the limit (castMaxDist 18 + spoolExtra 12 = 30 m) is measured from the rod tip, CastTipReach nearer.
            m.Tick(0.1f, Fr(p2b: FishingConstants.CastTipReach + 29.99f));   // (not exactly 30: the float sum would round above the limit)
            Assert.That(m.State, Is.EqualTo(RodState.Waiting), "just inside 30 m from the tip is the limit, not beyond it");
            Drain(m);
            m.Tick(0.1f, Fr(p2b: FishingConstants.CastTipReach + 30.5f));
            Assert.That(m.State, Is.EqualTo(RodState.Idle));
            List<RodEffect> e = Drain(m);
            Assert.That(Count(e, RodEffectKind.SendCancel), Is.EqualTo(1));
            Assert.That(HasCue(e, RodCue.Recall), Is.True);
        }

        [Test]
        public void RodStateMachine_Waiting_BiteTimerElapsed_EntersBiteAndSendsBiteEvent()
        {
            RodStateMachine m = New();
            ToWaiting(m);
            m.Tick(1.9f, Fr());
            Assert.That(m.State, Is.EqualTo(RodState.Waiting));
            Drain(m);
            m.Tick(0.2f, Fr());
            Assert.That(m.State, Is.EqualTo(RodState.Bite));
            List<RodEffect> e = Drain(m);
            Assert.That(HasCue(e, RodCue.Bite), Is.True);
            Assert.That(RodEv(e, RodEventType.Bite).CastId, Is.EqualTo(1u));
        }

        [Test]
        public void RodStateMachine_Bite_AutoHookAfterHookBeat_EntersHookedAndStartsFight()
        {
            RodStateMachine m = New();
            ToBite(m);
            m.Tick(0.2f, Fr());
            Assert.That(m.State, Is.EqualTo(RodState.Bite), "Bite at 0.2");
            Assert.That(Drain(m), Is.Empty);
            m.Tick(0.1f, Fr());
            Assert.That(m.State, Is.EqualTo(RodState.Hooked), "Hooked at 0.3");
            List<RodEffect> e = Drain(m);
            Assert.That(e.Count, Is.EqualTo(4));
            Assert.That(e[0].Kind, Is.EqualTo(RodEffectKind.Cue)); Assert.That(e[0].Cue, Is.EqualTo(RodCue.HookSet));
            Assert.That(e[1].Kind, Is.EqualTo(RodEffectKind.SendHooked)); Assert.That(e[1].CastId, Is.EqualTo(1u));
            Assert.That(e[2].Kind, Is.EqualTo(RodEffectKind.SendRodEvent)); Assert.That(e[2].EventType, Is.EqualTo(RodEventType.HookSet));
            Assert.That(e[3].Kind, Is.EqualTo(RodEffectKind.StartFight));
            Assert.That(m.Fight, Is.Not.Null);
            Assert.That(m.Fight.State.LineLength, Is.EqualTo(10f));
        }

        [Test]
        public void RodStateMachine_Bite_PrimaryInsideHookWindow_EntersHooked()
        {
            RodStateMachine m = New();
            ToBite(m, FishRoll(hookWindow: 1.0f));
            m.Tick(0.5f, Fr(pp: true));
            Assert.That(m.State, Is.EqualTo(RodState.Hooked));
            Assert.That(m.Fight, Is.Not.Null);
            List<RodEffect> e = Drain(m);
            Assert.That(First(e, RodEffectKind.SendHooked).CastId, Is.EqualTo(1u));
            Assert.That(Count(e, RodEffectKind.StartFight), Is.EqualTo(1));

            // Pity: the window is 1.3 s, so a press at 1.25 s still hooks (it would be Missed on the plain 1.0 s window).
            RodStateMachine pm = New();
            ToBite(pm, FishRoll(hookWindow: 1.0f, pity: true));
            pm.Tick(0.6f, Fr());
            pm.Tick(0.6f, Fr());
            Assert.That(pm.State, Is.EqualTo(RodState.Bite), "1.2 s");
            pm.Tick(0.05f, Fr(pp: true));
            Assert.That(pm.State, Is.EqualTo(RodState.Hooked), "press at 1.25 s hooks on a pity window");
            Assert.That(pm.Fight, Is.Not.Null);
            Assert.That(Count(Drain(pm), RodEffectKind.SendHooked), Is.EqualTo(1));
        }

        [Test]
        public void RodStateMachine_Bite_HookWindowElapsed_EscapesMissed()
        {
            RodStateMachine m = New();
            ToBite(m, FishRoll(hookWindow: 1.0f));
            m.Tick(0.6f, Fr());
            Assert.That(m.State, Is.EqualTo(RodState.Bite));
            Drain(m);
            m.Tick(0.6f, Fr());
            Assert.That(m.State, Is.EqualTo(RodState.Escaped));
            Assert.That(m.Reason, Is.EqualTo(EscapeReason.Missed));
            List<RodEffect> e = Drain(m);
            RodEffect res = First(e, RodEffectKind.SendResult);
            Assert.That(res.Outcome, Is.EqualTo(FightOutcome.Escaped));
            Assert.That(res.Reason, Is.EqualTo(EscapeReason.Missed));
            Assert.That(res.Report.MaxTension, Is.EqualTo(0f));
            Assert.That(res.Report.FightSeconds, Is.EqualTo(0f));
            Assert.That(res.Report.NearSnapRecoveries, Is.EqualTo(0));
            RodEffect esc = RodEv(e, RodEventType.Escaped);
            Assert.That(esc.CastId, Is.EqualTo(1u));
            Assert.That(esc.SizeClass, Is.EqualTo((byte)0), "Escaped events carry no size class (roll SizeClass is 1)");
            Assert.That(e.FindIndex(x => x.Kind == RodEffectKind.SendResult),
                        Is.LessThan(e.FindIndex(x => x.Kind == RodEffectKind.SendRodEvent && x.EventType == RodEventType.Escaped)),
                        "SendResult before SendRodEvent(Escaped)");

            // R65: a pity roll extends a real hook window by PityHookWindowBonus (0.3): 1.3 s in total.
            RodStateMachine pm = New();
            ToBite(pm, FishRoll(hookWindow: 1.0f, pity: true));
            pm.Tick(0.6f, Fr());
            pm.Tick(0.6f, Fr());
            Assert.That(pm.State, Is.EqualTo(RodState.Bite), "still Bite after 1.2 s");
            pm.Tick(0.1f, Fr());
            Assert.That(pm.State, Is.EqualTo(RodState.Escaped), "Missed after exactly 1.3 s");
            Assert.That(pm.Reason, Is.EqualTo(EscapeReason.Missed));

            // ... and a pity roll with hookWindow 0 auto-hooks at HookBeat as usual.
            RodStateMachine am = New();
            ToBite(am, FishRoll(hookWindow: 0f, pity: true));
            am.Tick(0.3f, Fr());
            Assert.That(am.State, Is.EqualTo(RodState.Hooked));
        }

        [Test]
        public void RodStateMachine_Hooked_LineAtLandDistanceWhileReeling_LandsWithFightReport()
        {
            RodStateMachine m = New();
            ToHooked(m, null, 2.05f);
            m.TickFight(In(0f, true), Fr());
            m.TickFight(In(0f, true), Fr());
            Assert.That(m.State, Is.EqualTo(RodState.Hooked));
            m.TickFight(In(0f, true), Fr());
            Assert.That(m.State, Is.EqualTo(RodState.Landed));
            Assert.That(m.Outcome, Is.EqualTo(FightOutcome.Landed));
            List<RodEffect> e = Drain(m);
            RodEffect res = First(e, RodEffectKind.SendResult);
            Assert.That(res.Outcome, Is.EqualTo(FightOutcome.Landed));
            Assert.That(res.Reason, Is.EqualTo(EscapeReason.None));
            Assert.That(res.Report.MaxTension, Is.EqualTo(0f));
            Assert.That(res.Report.FightSeconds, Is.EqualTo(0.06f).Within(1e-5f));
            Assert.That(res.Report.NearSnapRecoveries, Is.EqualTo(0));
            Assert.That(HasCue(e, RodCue.LineLanded), Is.True);

            // FightReport echo: the report carries the sim's MaxTension, NearSnapRecoveries and Elapsed (5 s + 3 ticks of 0.02 s).
            RodStateMachine r = New();
            ToHooked(r, null, 2.05f);
            var s = new FightState
            {
                LineLength = 2.05f, FishStamina = 1f, Phase = FightPhase.Calm, PhaseTimer = 5f,
                MaxTension = 0.7f, NearSnapRecoveries = 2, Elapsed = 5f
            };
            r.Fight.ForceState(in s);
            int n = 0;
            while (r.State == RodState.Hooked && n < 10) { r.TickFight(In(0f, true), Fr()); n++; }
            Assert.That(r.State, Is.EqualTo(RodState.Landed));
            RodEffect rr = First(Drain(r), RodEffectKind.SendResult);
            Assert.That(rr.Report.MaxTension, Is.EqualTo(0.7f).Within(1e-5f));
            Assert.That(rr.Report.NearSnapRecoveries, Is.EqualTo(2));
            Assert.That(rr.Report.FightSeconds, Is.EqualTo(5f + 0.02f * n).Within(1e-4f));
        }

        [Test]
        public void RodStateMachine_Hooked_SnapTimerReachesGrace_Snaps()
        {
            RodStateMachine m = New();
            var all = new List<RodEffect>();
            ToBite(m);
            m.Tick(0.3f, Fr());
            all.AddRange(Drain(m));                                   // hook effects: cue HookSet and friends
            ForceSnapState(m, 0.24f);
            m.TickFight(In(0f, true), Fr());
            Assert.That(m.State, Is.EqualTo(RodState.Snapped));
            Assert.That(m.Outcome, Is.EqualTo(FightOutcome.Snapped));
            List<RodEffect> e = Drain(m);
            all.AddRange(e);
            int iResult = e.FindIndex(x => x.Kind == RodEffectKind.SendResult);
            int iEvent = e.FindIndex(x => x.Kind == RodEffectKind.SendRodEvent);
            Assert.That(iResult, Is.GreaterThanOrEqualTo(0));
            Assert.That(iEvent, Is.GreaterThan(iResult), "SendResult before SendRodEvent");
            Assert.That(e[iResult].Outcome, Is.EqualTo(FightOutcome.Snapped));
            Assert.That(e[iEvent].EventType, Is.EqualTo(RodEventType.Snapped));
            Assert.That(e[iEvent].SizeClass, Is.EqualTo((byte)1));
            // BF-1: only the Snapped cue carries the size class.
            int snapCues = 0;
            foreach (RodEffect x in all)
            {
                if (x.Kind != RodEffectKind.Cue) continue;
                if (x.Cue == RodCue.Snapped) { snapCues++; Assert.That(x.SizeClass, Is.EqualTo((byte)1)); }
                else Assert.That(x.SizeClass, Is.EqualTo((byte)0), "cue " + x.Cue);
            }
            Assert.That(snapCues, Is.EqualTo(1));
        }

        [Test]
        public void RodStateMachine_Hooked_PinnedTooLong_EscapesPinned()
        {
            RodStateMachine m = New();
            ToHooked(m);
            var s = new FightState { LineLength = 10f, Lateral = 1f, FishStamina = 1f, Phase = FightPhase.Calm, PhaseTimer = 5f, PinnedTimer = 4.99f };
            m.Fight.ForceState(in s);
            m.TickFight(In(0f, false), Fr());
            Assert.That(m.State, Is.EqualTo(RodState.Escaped));
            Assert.That(m.Reason, Is.EqualTo(EscapeReason.Pinned));
            RodEffect res = First(Drain(m), RodEffectKind.SendResult);
            Assert.That(res.Outcome, Is.EqualTo(FightOutcome.Escaped));
            Assert.That(res.Reason, Is.EqualTo(EscapeReason.Pinned));
        }

        [Test]
        public void RodStateMachine_Hooked_SlackTooLong_EscapesThrewHook()
        {
            RodStateMachine m = New();
            ToHooked(m);
            var s = new FightState { LineLength = 10f, FishStamina = 1f, Phase = FightPhase.Calm, PhaseTimer = 5f, SlackTimer = 11.99f };
            m.Fight.ForceState(in s);
            m.TickFight(In(0f, false), Fr());
            Assert.That(m.State, Is.EqualTo(RodState.Escaped));
            Assert.That(m.Reason, Is.EqualTo(EscapeReason.ThrewHook));
            RodEffect res = First(Drain(m), RodEffectKind.SendResult);
            Assert.That(res.Reason, Is.EqualTo(EscapeReason.ThrewHook));
        }

        [Test]
        public void RodStateMachine_Hooked_DisplacedBeyondLeash_EscapesDropped()
        {
            RodStateMachine m = New();
            ToHooked(m);
            m.Tick(0.1f, Fr(disp: 3.0f));
            Assert.That(m.State, Is.EqualTo(RodState.Hooked), "3.0 m is the leash, not beyond it");
            m.Tick(0.1f, Fr(disp: 3.1f));
            Assert.That(m.State, Is.EqualTo(RodState.Escaped));
            Assert.That(m.Reason, Is.EqualTo(EscapeReason.Dropped));
            RodEffect res = First(Drain(m), RodEffectKind.SendResult);
            Assert.That(res.Reason, Is.EqualTo(EscapeReason.Dropped));
        }

        [Test]
        public void RodStateMachine_Hooked_EnteredWater_EscapesDropped()
        {
            RodStateMachine m = New();
            ToHooked(m);
            m.Tick(0.1f, Fr(water: true));
            Assert.That(m.State, Is.EqualTo(RodState.Escaped));
            Assert.That(m.Reason, Is.EqualTo(EscapeReason.Dropped));
        }

        static void AssertCancelledToIdle(RodStateMachine m)
        {
            Assert.That(m.State, Is.EqualTo(RodState.Idle));
            List<RodEffect> e = Drain(m);
            Assert.That(Count(e, RodEffectKind.SendCancel), Is.EqualTo(1));
            Assert.That(HasCue(e, RodCue.Recall), Is.True);
        }

        [Test]
        public void RodStateMachine_Waiting_ForceEndTeleport_CancelsToIdle()
        {
            RodStateMachine m = New();
            ToWaiting(m);
            m.ForceEnd(RodForceReason.Teleport);
            AssertCancelledToIdle(m);
        }

        [Test]
        public void RodStateMachine_Casting_ForceEndRespawn_CancelsToIdle()
        {
            RodStateMachine m = New();
            ToCasting(m);
            m.ForceEnd(RodForceReason.Respawn);
            AssertCancelledToIdle(m);
        }

        [Test]
        public void RodStateMachine_Bite_ForceEndStow_CancelsToIdle()
        {
            RodStateMachine m = New();
            ToBite(m);
            m.ForceEnd(RodForceReason.Stow);
            AssertCancelledToIdle(m);
        }

        [Test]
        public void RodStateMachine_Waiting_ForceEndSwim_CancelsToIdle()
        {
            RodStateMachine m = New();
            ToWaiting(m);
            m.ForceEnd(RodForceReason.Swim);
            AssertCancelledToIdle(m);
        }

        [Test]
        public void RodStateMachine_Hooked_ForceEndDisconnect_EscapesDropped()
        {
            RodStateMachine m = New();
            ToHooked(m);
            m.ForceEnd(RodForceReason.Disconnect);
            Assert.That(m.State, Is.EqualTo(RodState.Escaped));
            Assert.That(m.Reason, Is.EqualTo(EscapeReason.Dropped));
            RodEffect res = First(Drain(m), RodEffectKind.SendResult);
            Assert.That(res.Outcome, Is.EqualTo(FightOutcome.Escaped));
            Assert.That(res.Reason, Is.EqualTo(EscapeReason.Dropped));

            // Readying / Ready / Charging: Idle with no sends.
            RodStateMachine r1 = New();
            r1.Tick(0.1f, Fr(sp: true)); Drain(r1);
            r1.ForceEnd(RodForceReason.Disconnect);
            Assert.That(r1.State, Is.EqualTo(RodState.Idle)); Assert.That(Drain(r1), Is.Empty);
            RodStateMachine r2 = New();
            ToReady(r2);
            r2.ForceEnd(RodForceReason.Disconnect);
            Assert.That(r2.State, Is.EqualTo(RodState.Idle)); Assert.That(Drain(r2), Is.Empty);
            RodStateMachine r3 = New();
            ToCharging(r3);
            r3.ForceEnd(RodForceReason.Disconnect);
            Assert.That(r3.State, Is.EqualTo(RodState.Idle)); Assert.That(Drain(r3), Is.Empty);
        }

        [Test]
        public void RodStateMachine_Waiting_UnsolicitedCancelCommit_RecallBeatThenIdle()
        {
            RodStateMachine m = New();
            ToWaiting(m);
            m.OnCommit(1u, true, FightOutcome.Cancelled, EscapeReason.None);
            Assert.That(m.State, Is.EqualTo(RodState.Escaped));
            Assert.That(m.Recalling, Is.True);
            Assert.That(m.Outcome, Is.EqualTo(FightOutcome.Cancelled));
            List<RodEffect> e = Drain(m);
            Assert.That(HasCue(e, RodCue.Recall), Is.True);
            Assert.That(Count(e, RodEffectKind.SendResult), Is.EqualTo(0));
            Assert.That(Count(e, RodEffectKind.SendCancel), Is.EqualTo(0));
            m.Tick(1.4f, Fr());
            Assert.That(m.State, Is.EqualTo(RodState.Escaped), "still the beat at 1.4 s");
            m.Tick(0.2f, Fr());
            Assert.That(m.State, Is.EqualTo(RodState.Idle), "Idle at 1.6 s");
        }

        [Test]
        public void RodStateMachine_Bite_UnsolicitedCommit_BeatThenIdleNoResult()
        {
            RodStateMachine m = New();
            ToBite(m);
            m.OnCommit(1u, true, FightOutcome.Escaped, EscapeReason.Invalid);
            Assert.That(m.State, Is.EqualTo(RodState.Escaped));
            Assert.That(m.Reason, Is.EqualTo(EscapeReason.Invalid));
            List<RodEffect> e = Drain(m);
            Assert.That(RodEv(e, RodEventType.Escaped).CastId, Is.EqualTo(1u));
            Assert.That(Count(e, RodEffectKind.SendResult), Is.EqualTo(0));
            m.Tick(1.6f, Fr());
            Assert.That(m.State, Is.EqualTo(RodState.Idle));
        }

        [Test]
        public void RodStateMachine_Hooked_UnsolicitedEscapeCommit_SlackBeatThenIdle()
        {
            RodStateMachine m = New();
            ToHooked(m);
            m.OnCommit(1u, true, FightOutcome.Escaped, EscapeReason.Timeout);
            Assert.That(m.State, Is.EqualTo(RodState.Escaped));
            Assert.That(m.Reason, Is.EqualTo(EscapeReason.Timeout));
            Assert.That(Count(Drain(m), RodEffectKind.SendResult), Is.EqualTo(0));
            int before = m.IgnoredEventCount;
            m.TickFight(In(0f, true), Fr());
            Assert.That(m.IgnoredEventCount, Is.EqualTo(before + 1));
            Assert.That(m.State, Is.EqualTo(RodState.Escaped));
            m.Tick(1.6f, Fr());
            Assert.That(m.State, Is.EqualTo(RodState.Idle));
        }

        [Test]
        public void RodStateMachine_Terminal_BeatDoneAndCommit_ReturnsIdle()
        {
            RodStateMachine m = New();
            ToLanded(m);
            m.Tick(1.6f, Fr());
            Assert.That(m.State, Is.EqualTo(RodState.Landed), "no commit yet");
            m.OnCommit(1u, true, FightOutcome.Landed, EscapeReason.None);
            m.Tick(0.01f, Fr());
            Assert.That(m.State, Is.EqualTo(RodState.Idle));
        }

        [Test]
        public void RodStateMachine_Terminal_CommitBeforeBeatEnds_WaitsForBeat()
        {
            RodStateMachine m = New();
            ToLanded(m);
            m.OnCommit(1u, true, FightOutcome.Landed, EscapeReason.None);
            m.Tick(1.0f, Fr());
            Assert.That(m.State, Is.EqualTo(RodState.Landed), "at 1.0 s");
            m.Tick(0.6f, Fr());
            Assert.That(m.State, Is.EqualTo(RodState.Idle), "at 1.6 s");
        }

        [Test]
        public void RodStateMachine_Terminal_NoCommit_ReturnsIdleAfterCommitTimeout()
        {
            RodStateMachine m = New();
            ToSnapped(m);
            m.Tick(1.0f, Fr());
            m.Tick(1.0f, Fr());
            Assert.That(m.State, Is.EqualTo(RodState.Snapped), "at 2.0 s");
            Assert.That(Count(Drain(m), RodEffectKind.CommitTimedOut), Is.EqualTo(0));
            m.Tick(1.0f, Fr());
            Assert.That(m.State, Is.EqualTo(RodState.Idle), "at 3.0 s");
            Assert.That(Count(Drain(m), RodEffectKind.CommitTimedOut), Is.EqualTo(1));
        }


        // ---- T8 review follow-up (rulings R1, R2) and edge rules ----------------------------------------------

        [Test]
        public void RodStateMachine_UnsolicitedCommitInCasting_Recalls()
        {
            RodStateMachine m = New();
            ToCasting(m);
            m.OnCommit(1u, true, FightOutcome.Cancelled, EscapeReason.None);
            Assert.That(m.State, Is.EqualTo(RodState.Escaped));
            Assert.That(m.Outcome, Is.EqualTo(FightOutcome.Cancelled));
            Assert.That(m.Recalling, Is.True);
            Assert.That(m.IgnoredEventCount, Is.EqualTo(0));
            List<RodEffect> e = Drain(m);
            Assert.That(HasCue(e, RodCue.Recall), Is.True);
            Assert.That(Count(e, RodEffectKind.SendResult), Is.EqualTo(0));
            Assert.That(Count(e, RodEffectKind.SendCancel), Is.EqualTo(0));
            m.Tick(1.4f, Fr());
            Assert.That(m.State, Is.EqualTo(RodState.Escaped), "still the beat at 1.4 s");
            m.Tick(0.2f, Fr());
            Assert.That(m.State, Is.EqualTo(RodState.Idle), "Idle at 1.6 s, commit already received");
            Assert.That(m.HasResult, Is.False);

            // a late CastResult for the recalled cast is dropped (state is Idle by now), not applied
            FishingRoll r = FishRoll();
            FishFightProfile p = Carp();
            m.OnCastResult(1u, true, in r, in p);
            Assert.That(m.State, Is.EqualTo(RodState.Idle));
            Assert.That(m.HasResult, Is.False);
        }

        [Test]
        public void RodStateMachine_InvalidCastResult_HostCancelledCommitIsConsumedSilently()
        {
            RodStateMachine m = New();
            ToCasting(m);
            m.OnCastResult(1u, false, default(FishingRoll), default(FishFightProfile));
            Drain(m);
            m.OnCommit(1u, true, FightOutcome.Cancelled, EscapeReason.None);
            Assert.That(m.IgnoredEventCount, Is.EqualTo(0), "the host's Cancelled commit for a rejected cast is expected");
            Assert.That(m.State, Is.EqualTo(RodState.Idle));
            Assert.That(Drain(m), Is.Empty);
            // the memory is one-shot: a second commit for that id is a real illegal event
            m.OnCommit(1u, true, FightOutcome.Cancelled, EscapeReason.None);
            Assert.That(m.IgnoredEventCount, Is.EqualTo(1));

            // same from Waiting (a result arrived and was later invalidated)
            RodStateMachine w = New();
            ToWaiting(w);
            w.OnCastResult(1u, false, default(FishingRoll), default(FishFightProfile));
            Assert.That(w.State, Is.EqualTo(RodState.Idle));
            Drain(w);
            w.OnCommit(1u, true, FightOutcome.Cancelled, EscapeReason.None);
            Assert.That(w.IgnoredEventCount, Is.EqualTo(0));

            // and a fresh cast after it is unaffected: CastId 2 is not swallowed
            ToWaiting(w);
            Assert.That(w.CastId, Is.EqualTo(2u));
            w.OnCommit(2u, true, FightOutcome.Cancelled, EscapeReason.None);
            Assert.That(w.State, Is.EqualTo(RodState.Escaped), "the new cast's unsolicited commit still recalls");
        }

        [Test]
        public void RodStateMachine_UnsolicitedCancelledCommit_EmitsNoEscapedRodEvent()
        {
            RodState[] states = { RodState.Casting, RodState.Waiting, RodState.Bite, RodState.Hooked };
            foreach (RodState s in states)
            {
                RodStateMachine m = New();
                DriveTo(m, s);
                m.OnCommit(1u, true, FightOutcome.Cancelled, EscapeReason.None);
                List<RodEffect> e = Drain(m);
                Assert.That(Count(e, RodEffectKind.SendRodEvent), Is.EqualTo(0), "no RodEvent broadcast in " + s);
                Assert.That(HasCue(e, RodCue.Escaped), Is.False, "no Escaped cue in " + s);
                Assert.That(HasCue(e, RodCue.Recall), Is.True, "Recall cue in " + s);
                Assert.That(m.Outcome, Is.EqualTo(FightOutcome.Cancelled), "outcome in " + s);
            }
        }

        [Test]
        public void RodStateMachine_UnsolicitedEscapedCommit_ReasonNoneBecomesInvalid()
        {
            RodStateMachine m = New();
            ToWaiting(m);
            m.OnCommit(1u, true, FightOutcome.Escaped, EscapeReason.None);
            Assert.That(m.Reason, Is.EqualTo(EscapeReason.Invalid));
            Assert.That(RodEv(Drain(m), RodEventType.Escaped).SizeClass, Is.EqualTo((byte)0));

            RodStateMachine c = New();
            ToCasting(c);
            c.OnCommit(1u, true, FightOutcome.Escaped, EscapeReason.None);
            Assert.That(c.State, Is.EqualTo(RodState.Escaped), "Casting recalls too (R1)");
            Assert.That(c.Reason, Is.EqualTo(EscapeReason.Invalid));
        }

        [Test]
        public void RodStateMachine_DuplicateCommitInTerminal_IgnoredSafely()
        {
            RodStateMachine m = New();
            ToLanded(m);
            m.OnCommit(1u, true, FightOutcome.Landed, EscapeReason.None);
            Assert.That(m.IgnoredEventCount, Is.EqualTo(0));
            m.OnCommit(1u, true, FightOutcome.Escaped, EscapeReason.Timeout);   // duplicate with different content
            Assert.That(m.IgnoredEventCount, Is.EqualTo(1));
            Assert.That(m.State, Is.EqualTo(RodState.Landed));
            Assert.That(m.Outcome, Is.EqualTo(FightOutcome.Landed), "first commit's content stands");
            Assert.That(m.Reason, Is.EqualTo(EscapeReason.None));
            Assert.That(Drain(m), Is.Empty);
            m.Tick(1.6f, Fr());
            Assert.That(m.State, Is.EqualTo(RodState.Idle));
            Assert.That(Count(Drain(m), RodEffectKind.CommitTimedOut), Is.EqualTo(0));
        }

        [Test]
        public void RodStateMachine_Paused_TickFightAndCastResult()
        {
            // TickFight while paused: fight does not advance, nothing is counted as illegal.
            RodStateMachine m = New();
            ToHooked(m);
            float before = m.Fight.State.Elapsed;
            m.TickFight(In(0f, true), Fr(paused: true));
            Assert.That(m.Fight.State.Elapsed, Is.EqualTo(before), "paused: the fight sim is not stepped");
            Assert.That(m.IgnoredEventCount, Is.EqualTo(0));
            m.TickFight(In(0f, true), Fr());
            Assert.That(m.Fight.State.Elapsed, Is.GreaterThan(before), "control: unpaused steps it");

            // paused TickFight in a state with no fight is not an illegal event either (the pause check comes first)
            RodStateMachine idle = New();
            idle.TickFight(In(0f, true), Fr(paused: true));
            Assert.That(idle.IgnoredEventCount, Is.EqualTo(0));

            // OnCastResult while paused: still accepted, but the flight and timeout clocks do not move until unpaused.
            RodStateMachine c = New();
            ToCasting(c);
            c.Tick(5f, Fr(paused: true));
            Assert.That(c.State, Is.EqualTo(RodState.Casting), "5 s of pause does not hit the 3 s result timeout");
            Assert.That(Drain(c), Is.Empty);
            FishingRoll r = FishRoll();
            FishFightProfile p = Carp();
            c.OnCastResult(1u, true, in r, in p);
            Assert.That(c.HasResult, Is.True);
            Assert.That(c.IgnoredEventCount, Is.EqualTo(0));
            c.Tick(5f, Fr(paused: true));
            Assert.That(c.State, Is.EqualTo(RodState.Casting), "flight does not progress while paused");
            c.Tick(0.6f, Fr());
            Assert.That(c.State, Is.EqualTo(RodState.Waiting));
        }

        [Test]
        public void RodStateMachine_ForceEnd_NoOpInTerminalStates()
        {
            RodForceReason[] reasons = { RodForceReason.Disconnect, RodForceReason.Teleport, RodForceReason.Respawn };
            RodState[] states = { RodState.Idle, RodState.Landed, RodState.Snapped, RodState.Escaped };
            foreach (RodState s in states)
            {
                foreach (RodForceReason why in reasons)
                {
                    RodStateMachine m = New();
                    DriveTo(m, s);
                    FightOutcome outcome = m.Outcome;
                    EscapeReason reason = m.Reason;
                    m.ForceEnd(why);
                    Assert.That(m.State, Is.EqualTo(s), s + "/" + why);
                    Assert.That(m.Outcome, Is.EqualTo(outcome), s + "/" + why + " outcome");
                    Assert.That(m.Reason, Is.EqualTo(reason), s + "/" + why + " reason");
                    Assert.That(Drain(m), Is.Empty, s + "/" + why + " effects (no second result, no cancel)");
                    Assert.That(m.IgnoredEventCount, Is.EqualTo(0));
                }
            }

            // also the recall beat (Escaped with Cancelled outcome): a no-op
            RodStateMachine rb = New();
            ToWaiting(rb);
            rb.OnCommit(1u, true, FightOutcome.Cancelled, EscapeReason.None);
            Drain(rb);
            rb.ForceEnd(RodForceReason.Respawn);
            Assert.That(rb.State, Is.EqualTo(RodState.Escaped));
            Assert.That(rb.Outcome, Is.EqualTo(FightOutcome.Cancelled));
            Assert.That(Drain(rb), Is.Empty);
        }

        // ---- rules ------------------------------------------------------------------------------------------

        static readonly RodState[] AllStates =
        {
            RodState.Idle, RodState.Readying, RodState.Ready, RodState.Charging, RodState.Casting, RodState.Waiting,
            RodState.Bite, RodState.Hooked, RodState.Landed, RodState.Snapped, RodState.Escaped
        };

        static void DriveTo(RodStateMachine m, RodState s)
        {
            switch (s)
            {
                case RodState.Idle: break;
                case RodState.Readying: m.Tick(0.1f, Fr(sp: true)); break;
                case RodState.Ready: ToReady(m); break;
                case RodState.Charging: ToCharging(m); break;
                case RodState.Casting: ToCasting(m); break;
                case RodState.Waiting: ToWaiting(m); break;
                case RodState.Bite: ToBite(m); break;
                case RodState.Hooked: ToHooked(m); break;
                case RodState.Landed: ToLanded(m); break;
                case RodState.Snapped: ToSnapped(m); break;
                case RodState.Escaped: ToEscaped(m); break;
            }
            Drain(m);
            Assert.That(m.State, Is.EqualTo(s));
        }

        [TestCaseSource(nameof(AllStates))]
        public void RodStateMachine_IllegalEvents_IgnoredInEveryState(RodState s)
        {
            RodStateMachine m = New();
            DriveTo(m, s);
            uint id = m.CastId;
            FishingRoll r = FishRoll();
            FishFightProfile p = Carp();
            int fired = 0;

            // every state: events for a different cast id
            m.OnCastResult(id + 1u, true, in r, in p); fired++;
            m.OnCommit(id + 1u, true, FightOutcome.Escaped, EscapeReason.None); fired++;

            bool preCast = s == RodState.Idle || s == RodState.Readying || s == RodState.Ready || s == RodState.Charging;
            if (preCast)
            {
                m.OnCastResult(id, true, in r, in p); fired++;
                m.OnCommit(id, true, FightOutcome.Escaped, EscapeReason.None); fired++;
                m.TickFight(In(0f, true), Fr()); fired++;
            }
            else if (s == RodState.Casting)
            {
                // an unsolicited commit while Casting is not illegal (T8-R1: it recalls), so it is not fired here
                m.TickFight(In(0f, true), Fr()); fired++;
            }
            else if (s == RodState.Waiting)
            {
                m.OnCastResult(id, true, in r, in p); fired++;        // duplicate valid result
                m.TickFight(In(0f, true), Fr()); fired++;
            }
            else if (s == RodState.Bite)
            {
                m.OnCastResult(id, true, in r, in p); fired++;
                m.TickFight(In(0f, true), Fr()); fired++;
            }
            else if (s == RodState.Hooked)
            {
                m.OnCastResult(id, true, in r, in p); fired++;
            }
            else   // Landed, Snapped, Escaped
            {
                m.OnCastResult(id, true, in r, in p); fired++;
                m.TickFight(In(0f, true), Fr()); fired++;
            }

            if (s != RodState.Idle)
            {
                RodStats twig = FishingTestData.Rods()[FishingTestData.RodTwig];
                m.SetRod(in twig); fired++;
            }

            Assert.That(m.State, Is.EqualTo(s), "state unchanged");
            Assert.That(Drain(m), Is.Empty, "no effects");
            Assert.That(m.IgnoredEventCount, Is.EqualTo(fired));
        }

        [Test]
        public void RodStateMachine_HeldPrimaryThroughCatch_DoesNotStartNewCast()
        {
            RodStateMachine m = New();
            ToCasting(m);
            FishingRoll r = FishRoll();
            FishFightProfile p = Carp();
            m.OnCastResult(1u, true, in r, in p);
            m.Tick(0.6f, Fr(ph: true));
            for (int i = 0; i < 8 && m.State == RodState.Waiting; i++) m.Tick(0.5f, Fr(ph: true));
            Assert.That(m.State, Is.EqualTo(RodState.Bite));
            m.Tick(0.3f, Fr(ph: true, t2b: 2.05f));
            Assert.That(m.State, Is.EqualTo(RodState.Hooked));
            for (int i = 0; i < 3; i++) m.TickFight(In(0f, true), Fr(ph: true));
            Assert.That(m.State, Is.EqualTo(RodState.Landed));
            m.OnCommit(1u, true, FightOutcome.Landed, EscapeReason.None);
            m.Tick(1.6f, Fr(ph: true));
            Assert.That(m.State, Is.EqualTo(RodState.Idle));
            Drain(m);

            m.Tick(0.1f, Fr(sp: true, ph: true));
            for (int i = 0; i < 3; i++) m.Tick(0.1f, Fr(ph: true));
            Assert.That(m.State, Is.EqualTo(RodState.Ready));
            m.Tick(1.0f, Fr(ph: true));
            Assert.That(m.State, Is.EqualTo(RodState.Ready), "no PrimaryPressed edge seen in Ready");
            Assert.That(Count(Drain(m), RodEffectKind.SendCastRequest), Is.EqualTo(0));
            Assert.That(m.CastId, Is.EqualTo(1u));
        }

        [Test]
        public void RodStateMachine_MismatchedCastId_Ignored()
        {
            RodStateMachine m = New();
            ToCasting(m);
            FishingRoll r = FishRoll();
            FishFightProfile p = Carp();
            m.OnCastResult(2u, true, in r, in p);
            m.OnCommit(0u, true, FightOutcome.Escaped, EscapeReason.None);
            Assert.That(m.HasResult, Is.False);
            Assert.That(m.State, Is.EqualTo(RodState.Casting));
            Assert.That(m.IgnoredEventCount, Is.EqualTo(2));
        }

        [Test]
        public void RodStateMachine_Paused_StopsBiteTimerAndBeat()
        {
            RodStateMachine m = New();
            ToWaiting(m);
            m.Tick(0.5f, Fr());
            Assert.That(m.BiteTimer, Is.EqualTo(0.5f).Within(Eps));
            float st = m.StateTime;
            m.Tick(5f, Fr(paused: true));
            Assert.That(m.BiteTimer, Is.EqualTo(0.5f).Within(Eps));
            Assert.That(m.StateTime, Is.EqualTo(st));
            Assert.That(m.State, Is.EqualTo(RodState.Waiting));

            RodStateMachine l = New();
            ToLanded(l);
            l.Tick(5f, Fr(paused: true));
            Assert.That(l.State, Is.EqualTo(RodState.Landed));
            l.OnCommit(1u, true, FightOutcome.Landed, EscapeReason.None);   // still applies while paused
            l.Tick(5f, Fr(paused: true));
            Assert.That(l.State, Is.EqualTo(RodState.Landed));
            l.Tick(1.6f, Fr());
            Assert.That(l.State, Is.EqualTo(RodState.Idle));
        }

        [Test]
        public void RodStateMachine_NibbleInput_NeverHooksOrCancels()
        {
            RodStateMachine m = New();
            ToWaiting(m, FishRoll(biteWait: 2f, nibbles: 2, n0: 0.6f, n1: 1.2f));
            var all = new List<RodEffect>();
            for (int i = 0; i < 19; i++)
            {
                m.Tick(0.1f, Fr(pp: true, ph: true));
                Assert.That(m.State, Is.EqualTo(RodState.Waiting), "tick " + i);
                all.AddRange(Drain(m));
            }
            Assert.That(CueCount(all, RodCue.Nibble), Is.EqualTo(2));
            int events = 0;
            foreach (RodEffect x in all) if (x.Kind == RodEffectKind.SendRodEvent && x.EventType == RodEventType.Nibble) events++;
            Assert.That(events, Is.EqualTo(2));
            Assert.That(Count(all, RodEffectKind.SendHooked), Is.EqualTo(0));
            Assert.That(Count(all, RodEffectKind.SendCancel), Is.EqualTo(0));
        }

        [Test]
        public void RodStateMachine_SetRod_IgnoredOutsideIdle()
        {
            RodStateMachine m = New();
            ToReady(m);
            RodStats twig = FishingTestData.Rods()[FishingTestData.RodTwig];
            m.SetRod(in twig);
            ToCharging(m);
            m.Tick(1.0f, Fr(ph: true));
            m.Tick(0.0001f, Fr(pr: true));
            Assert.That(m.State, Is.EqualTo(RodState.Casting));
            Assert.That(m.CastDistance, Is.EqualTo(18f).Within(0.01f), "Handmade, not Twig (14)");
            Assert.That(m.IgnoredEventCount, Is.EqualTo(1));
        }

        // Ticks (1-based) a forced Burst/Tension-1 fight takes to snap, reeling all the way.
        static int SnapTick(RodStateMachine m)
        {
            ForceSnapState(m, 0f);
            for (int i = 1; i <= 80; i++)
            {
                m.TickFight(In(0f, true), Fr());
                if (m.State == RodState.Snapped) return i;
            }
            return -1;
        }

        [Test]
        public void RodStateMachine_Assist_CapturedAtRelease()
        {
            RodStateMachine m = New();
            m.Assist = false;
            ToCasting(m);                 // release captures Assist = false
            m.Assist = true;              // changed later: affects only the next cast
            ToHooked(m);
            Assert.That(SnapTick(m), Is.EqualTo(13), "no-assist grace (0.25 s)");

            RodStateMachine a = New();
            a.Assist = true;
            ToHooked(a);
            Assert.That(SnapTick(a), Is.EqualTo(38), "assist grace (0.75 s) when set before release");
        }
    }
}
