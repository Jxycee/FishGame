// Core/Fishing/RodStateMachine.cs - owner-run rod state machine (spec 9.2; C 7.8). Pure C#.
// Authority: the owning client runs it; the host only validates (FishingHostLedger). Row map: plan T8 / C 10.2.
using System;
using System.Collections.Generic;

namespace FishGame.Core
{
    public sealed class RodStateMachine
    {
        private const float Eps = 1e-4f;
        private readonly FishingTuning _t;
        private readonly List<RodEffect> _effects = new List<RodEffect>(8);
        private RodStats _rod;
        private FishFightProfile _profile;
        private float _chargeHeld;
        private bool _flightOver;        // Landing cue emitted for this cast
        private int _nibblesDone;
        private bool _commitReceived;
        private bool _castAssist;        // Assist captured at release
        private uint _cancelledCastId;   // last cast we sent SendCancel for (0 = none); never cleared by ResetCast

        public RodStateMachine(in FishingTuning tuning) { _t = tuning; }

        public RodState State { get; private set; }
        public float StateTime { get; private set; }
        public uint CastId { get; private set; }
        public float ChargeNorm { get; private set; }
        public float CastDistance { get; private set; }
        public bool MarkerOn => State == RodState.Ready || State == RodState.Charging;
        public bool HasResult { get; private set; }
        public FishingRoll Roll { get; private set; }
        public float BiteTimer { get; private set; }
        public float BiteWait { get; private set; }
        public FightSim Fight { get; private set; }
        public FightOutcome Outcome { get; private set; }
        public EscapeReason Reason { get; private set; }
        public bool Recalling { get; private set; }
        public bool Assist { get; set; }
        public int IgnoredEventCount { get; private set; }

        public void SetRod(in RodStats rod)
        {
            if (State != RodState.Idle) { IgnoredEventCount++; return; }
            _rod = rod;
        }

        public void Tick(float dt, in RodFrame f)
        {
            if (f.Paused || !(dt > 0f)) return;
            StateTime += dt;
            switch (State)
            {
                case RodState.Idle:                                                    // row: Idle / Secondary down
                    if (f.SecondaryPressed) Enter(RodState.Readying);
                    break;
                case RodState.Readying:
                    if (!f.SecondaryHeld) Enter(RodState.Idle);                        // row: Readying / released
                    else if (StateTime >= FishingConstants.ReadyHoldSeconds - Eps)    // row: Readying / held 0.25 s
                    { Enter(RodState.Ready); Cue(RodCue.Ready); }
                    break;
                case RodState.Ready:
                    if (!f.SecondaryHeld) Enter(RodState.Idle);                        // row: Ready / released
                    else if (f.PrimaryPressed)                                         // row: Ready / fresh Primary
                    { Enter(RodState.Charging); _chargeHeld = 0f; ChargeNorm = 0f; Cue(RodCue.ChargeStart); }
                    break;
                case RodState.Charging: TickCharging(dt, f); break;
                case RodState.Casting: TickCasting(); break;
                case RodState.Waiting: TickWaiting(dt, f); break;
                case RodState.Bite: TickBite(f); break;
                case RodState.Hooked:                                                  // rows: Hooked / leash, entered water
                    if (f.InWater || f.HookDisplacement > _t.HookedLeash) EndEscaped(EscapeReason.Dropped);
                    break;
                default: TickTerminal(); break;                                        // Landed, Snapped, Escaped
            }
        }

        private void TickCharging(float dt, in RodFrame f)
        {
            if (!f.SecondaryHeld) { Enter(RodState.Idle); ChargeNorm = 0f; return; }  // row: Charging / Secondary released
            _chargeHeld += dt;
            ChargeNorm = CastMath.ChargeNorm(_chargeHeld, _t.CastChargeTime, _t.ChargePingPong);
            if (f.PrimaryHeld && !f.PrimaryReleased) return;
            if (ChargeNorm >= FishingConstants.MinChargeNorm && f.MarkerValid)        // row: Charging / valid release
            {
                CastId++;
                CastDistance = CastMath.Distance(ChargeNorm, _t.CastMinDist, _rod.CastMaxDist);
                _castAssist = Assist;
                ResetCast();
                Enter(RodState.Casting);
                Cue(RodCue.CastRelease);
                Add(new RodEffect { Kind = RodEffectKind.SendCastRequest, CastId = CastId, ChargeNorm = ChargeNorm });
            }
            else { Enter(RodState.Ready); ChargeNorm = 0f; Cue(RodCue.InvalidRelease); }  // rows: short / invalid marker
        }

        private void TickCasting()
        {
            if (!_flightOver && StateTime >= CastMath.FlightTime(CastDistance)) { _flightOver = true; Cue(RodCue.Landing); }
            if (_flightOver && HasResult) { EnterWaiting(); return; }                 // row: flight over + result
            if (!HasResult && StateTime >= _t.ResultTimeout) Recall(sendCancel: true); // row: no result in 3 s
            // otherwise: flight over, no result -> stay Casting, bite timer not running (row)
        }

        private void TickWaiting(float dt, in RodFrame f)
        {
            if (f.SecondaryPressed) { Recall(sendCancel: true); return; }             // row: Waiting / Secondary pressed
            if (f.PlayerToBobber > _rod.MaxLineOut(_t.SpoolExtra)) { Recall(sendCancel: true); return; } // row: maxLineOut
            BiteTimer += dt;
            while (_nibblesDone < Roll.NibbleCount && _nibblesDone < FishingConstants.MaxNibbleSlots
                   && BiteTimer >= NibbleAt(_nibblesDone))
            { Cue(RodCue.Nibble); RodEvent(RodEventType.Nibble, 0); _nibblesDone++; }  // nibbles never hook or cancel
            if (BiteTimer >= BiteWait)                                                // row: bite timer
            { Enter(RodState.Bite); Cue(RodCue.Bite); RodEvent(RodEventType.Bite, 0); }
        }

        private void TickBite(in RodFrame f)
        {
            float window = Roll.HookWindow > 0f && Roll.Pity ? Roll.HookWindow + _t.PityHookWindowBonus : Roll.HookWindow;   // R65: pity extends a real hook window; auto-hook (0) is unchanged
            if (window > 0f)
            {
                if (f.PrimaryPressed) Hook(f);                                         // row: Primary in window
                else if (StateTime >= window) EndEscaped(EscapeReason.Missed);         // row: window elapsed
            }
            else if (StateTime >= _t.HookBeat - Eps) Hook(f);                          // row: auto-hook
        }

        private void Hook(in RodFrame f)
        {
            Enter(RodState.Hooked);
            Fight = Roll.Catch.IsJunk
                ? FightSim.ForJunk(_rod, _t, Roll.FightSeed, f.TipToBobber)
                : new FightSim(_profile, _rod, _t, Roll.WeightT, Roll.FightSeed, f.TipToBobber, _castAssist, Roll.Pity);
            Cue(RodCue.HookSet);
            Add(new RodEffect { Kind = RodEffectKind.SendHooked, CastId = CastId });
            RodEvent(RodEventType.HookSet, 0);
            Add(new RodEffect { Kind = RodEffectKind.StartFight, CastId = CastId });
        }

        public void TickFight(in FightInput input, in RodFrame f)
        {
            if (f.Paused) return;
            if (State != RodState.Hooked || Fight == null) { IgnoredEventCount++; return; }
            FightPhase phase = Fight.State.Phase;
            sbyte dir = Fight.State.BurstDir;
            bool wasExhausted = Fight.Exhausted;
            FightOutcome o = Fight.Tick(input);
            if (o == FightOutcome.None)
            {
                if (Fight.IsJunk) return;
                FightPhase now = Fight.State.Phase;
                if (Fight.Exhausted && !wasExhausted) Cue(RodCue.Exhausted);
                else if (now == FightPhase.Calm && phase == FightPhase.Burst) Cue(RodCue.Calm);
                else if (now == FightPhase.Burst && (phase == FightPhase.Calm || Fight.State.BurstDir != dir))
                    Cue(Fight.State.BurstDir == 0 ? RodCue.Dive : RodCue.SideRun);  // new run or a flip
                return;
            }
            FightReport report = ReportOf(Fight);
            if (o == FightOutcome.Landed)                                              // row: Hooked / land
            {
                EnterTerminal(RodState.Landed, FightOutcome.Landed, EscapeReason.None);
                SendResult(FightOutcome.Landed, EscapeReason.None, report);
                Cue(RodCue.LineLanded);
            }
            else if (o == FightOutcome.Snapped)                                        // row: Hooked / snap
            {
                EnterTerminal(RodState.Snapped, FightOutcome.Snapped, EscapeReason.None);
                SendResult(FightOutcome.Snapped, EscapeReason.None, report);
                RodEvent(RodEventType.Snapped, Roll.SizeClass);
                Cue(RodCue.Snapped);
            }
            else EndEscaped(Fight.State.Reason);                                       // rows: pinned, slack
        }

        private void TickTerminal()
        {
            if (StateTime < FishingConstants.FeedbackBeatSeconds - Eps) return;       // 1.5 s beat first
            if (_commitReceived) { EnterIdle(); return; }                              // row: beat + commit
            if (StateTime >= _t.CommitTimeout - Eps)                                   // row: no commit in 3 s
            { Add(new RodEffect { Kind = RodEffectKind.CommitTimedOut, CastId = CastId }); EnterIdle(); }
        }

        public void OnCastResult(uint castId, bool valid, in FishingRoll roll, in FishFightProfile profile)
        {
            if (_cancelledCastId != 0 && castId == _cancelledCastId) return;           // late reply to our own cancel
            if (castId != CastId || (State != RodState.Casting && State != RodState.Waiting)) { IgnoredEventCount++; return; }
            if (!valid) { _cancelledCastId = castId; Recall(sendCancel: false); return; } // rows: Casting / Waiting invalid; the host's Cancelled commit is expected (T8-R2)
            if (State == RodState.Waiting || HasResult) { IgnoredEventCount++; return; } // duplicate
            HasResult = true; Roll = roll; _profile = profile; BiteWait = roll.BiteWait;
            if (_flightOver) EnterWaiting();                                           // timer starts at the result
        }

        public void OnCommit(uint castId, bool accepted, FightOutcome finalOutcome, EscapeReason finalReason)
        {
            if (_cancelledCastId != 0 && castId == _cancelledCastId) { _cancelledCastId = 0; return; } // our cancel's reply
            if (castId != CastId) { IgnoredEventCount++; return; }
            switch (State)
            {
                case RodState.Landed: case RodState.Snapped: case RodState.Escaped:
                    if (_commitReceived) IgnoredEventCount++; else _commitReceived = true;
                    return;
                case RodState.Casting: case RodState.Waiting: case RodState.Bite: case RodState.Hooked: // rows: unsolicited commit (Casting: T8-R1)
                    if (finalOutcome == FightOutcome.Cancelled)
                    {
                        EnterTerminal(RodState.Escaped, FightOutcome.Cancelled, finalReason);
                        Recalling = true;
                        Cue(RodCue.Recall);
                    }
                    else
                    {
                        EnterTerminal(RodState.Escaped, FightOutcome.Escaped,
                                      finalReason == EscapeReason.None ? EscapeReason.Invalid : finalReason);
                        RodEvent(RodEventType.Escaped, 0);
                        Cue(RodCue.Escaped);
                    }
                    _commitReceived = true;
                    return;
                default:
                    IgnoredEventCount++;
                    return;
            }
        }

        public void ForceEnd(RodForceReason reason)                                    // row: any line-out state
        {
            switch (State)
            {
                case RodState.Readying: case RodState.Ready: case RodState.Charging: EnterIdle(); return;
                case RodState.Casting: case RodState.Waiting: case RodState.Bite: Recall(sendCancel: true); return;
                case RodState.Hooked: EndEscaped(EscapeReason.Dropped); return;
                default: return;                                                       // Idle and terminal: no-op
            }
        }

        public int DrainEffects(List<RodEffect> into)
        {
            if (into == null) throw new ArgumentNullException(nameof(into));
            int n = _effects.Count;
            into.AddRange(_effects);
            _effects.Clear();
            return n;
        }

        private void EndEscaped(EscapeReason reason)
        {
            FightReport report = Fight != null ? ReportOf(Fight) : default;
            EnterTerminal(RodState.Escaped, FightOutcome.Escaped, reason);
            SendResult(FightOutcome.Escaped, reason, report);
            RodEvent(RodEventType.Escaped, 0);
            Cue(RodCue.Escaped);
        }

        private void Recall(bool sendCancel)
        {
            if (sendCancel)
            {
                Add(new RodEffect { Kind = RodEffectKind.SendCancel, CastId = CastId });
                _cancelledCastId = CastId;   // the host's CommitRpc (and any late CastResult) for it is expected, not illegal
            }
            Cue(RodCue.Recall);
            EnterIdle();
        }

        private void EnterWaiting() { Enter(RodState.Waiting); BiteTimer = 0f; _nibblesDone = 0; }
        private void EnterTerminal(RodState s, FightOutcome o, EscapeReason r)
        { Enter(s); Outcome = o; Reason = r; Recalling = false; _commitReceived = false; }
        private void EnterIdle() { Enter(RodState.Idle); ResetCast(); ChargeNorm = 0f; }
        private void Enter(RodState s) { State = s; StateTime = 0f; }

        private void ResetCast()
        {
            HasResult = false; Roll = default; BiteTimer = 0f; BiteWait = 0f; Fight = null;
            Outcome = FightOutcome.None; Reason = EscapeReason.None; Recalling = false;
            _flightOver = false; _nibblesDone = 0; _commitReceived = false;
        }

        private float NibbleAt(int k) => k == 0 ? Roll.Nibble0 : (k == 1 ? Roll.Nibble1 : Roll.Nibble2);
        private static FightReport ReportOf(FightSim s) => new FightReport
        { MaxTension = s.State.MaxTension, FightSeconds = s.State.Elapsed, NearSnapRecoveries = s.State.NearSnapRecoveries };
        // BF-1: only the Snapped cue carries the size class (B-Q13); the owner's local cue reads it (net ProcessEffects).
        private void Cue(RodCue c) =>
            Add(new RodEffect { Kind = RodEffectKind.Cue, Cue = c, CastId = CastId, SizeClass = c == RodCue.Snapped ? Roll.SizeClass : (byte)0 });
        private void RodEvent(RodEventType t, byte size) =>
            Add(new RodEffect { Kind = RodEffectKind.SendRodEvent, EventType = t, SizeClass = size, CastId = CastId });
        private void SendResult(FightOutcome o, EscapeReason r, in FightReport rep) =>
            Add(new RodEffect { Kind = RodEffectKind.SendResult, CastId = CastId, Outcome = o, Reason = r, Report = rep });
        private void Add(in RodEffect e) => _effects.Add(e);
    }
}
