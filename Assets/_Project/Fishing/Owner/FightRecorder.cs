// Authority: owner only (plain C#, no MonoBehaviour). Collects the telemetry of one cast for FishingRodController so EditMode can test it.
// RodStateMachine.ResetCast wipes Roll, Outcome and Fight when it enters Idle, and a recall goes straight from Waiting/Bite to Idle in one
// call, so the controller must capture the roll on entering Waiting and the outcome on entering a terminal state; a cast that reaches Idle
// without a terminal state was cancelled (SendCancel / Recall) and is recorded as Cancelled (T8 note).
using FishGame.Core;

namespace FishGame.Fishing
{
    internal sealed class FightRecorder
    {
        private bool _reached, _bitten, _hasTerminal, _assist;
        private float _releaseAt, _biteAt, _castDistance, _fightSeconds, _maxTension;
        private FishingRoll _roll;
        private FightOutcome _outcome;
        private EscapeReason _reason;
        private int _nearSnap;

        public bool ReachedWaiting => _reached;
        public FishingRoll Roll => _roll;

        public void Reset()
        {
            _reached = _bitten = _hasTerminal = _assist = false;
            _releaseAt = _biteAt = _castDistance = _fightSeconds = _maxTension = 0f;
            _roll = default;
            _outcome = FightOutcome.None;
            _reason = EscapeReason.None;
            _nearSnap = 0;
        }

        // SendCastRequest: starts a fresh record. `assist` is the value the machine captured at release.
        public void OnCastRequested(float clock, bool assist)
        {
            Reset();
            _releaseAt = clock;
            _assist = assist;
        }

        // Casting -> Waiting: the roll is still on the machine now (ResetCast wipes it later).
        public void OnWaiting(in FishingRoll roll, float castDistance)
        {
            _reached = true;
            _roll = roll;
            _castDistance = castDistance;
        }

        public void OnBite(float clock)
        {
            if (!_reached || _bitten) return;
            _bitten = true;
            _biteAt = clock;
        }

        // Entering Landed / Snapped / Escaped. Zeros when no fight ran (Missed, unsolicited commit before the hook).
        public void OnTerminal(FightOutcome outcome, EscapeReason reason, float maxTension, float fightSeconds, int nearSnapRecoveries)
        {
            _hasTerminal = true;
            _outcome = outcome;
            _reason = reason;
            _maxTension = maxTension;
            _fightSeconds = fightSeconds;
            _nearSnap = nearSnapRecoveries;
        }

        // Entering Idle (or the controller going away). Returns false and clears when the cast never reached Waiting.
        // Without a captured terminal state the cast was cancelled.
        public bool TryFinish(float clock, string profileId, string itemId, string rodId, float cleanCatchMaxTension, out FightEnded result)
        {
            if (!_reached)
            {
                Reset();
                result = default;
                return false;
            }

            FightOutcome outcome = _hasTerminal ? _outcome : FightOutcome.Cancelled;
            EscapeReason reason = _hasTerminal ? _reason : EscapeReason.None;
            float castToBite = (_bitten ? _biteAt : clock) - _releaseAt;
            if (castToBite < 0f) castToBite = 0f;
            bool clean = outcome == FightOutcome.Landed && !_roll.Catch.IsJunk && _maxTension < cleanCatchMaxTension;
            result = new FightEnded(profileId, itemId, rodId, _roll.Catch.WeightG, _castDistance, castToBite, _fightSeconds,
                                    outcome, reason, _nearSnap, _maxTension, _assist, clean, _roll.Pity);
            Reset();
            return true;
        }
    }
}
