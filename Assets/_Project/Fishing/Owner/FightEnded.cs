// Authority: owner only. Telemetry payload raised by FishingRodController (C# event FightEnded) and published on the App EventBus,
// one per cast that reached Waiting (spec 9.12 fight log). Consumed by FightTelemetry (T27). Plain data, no behaviour.
using FishGame.Core;

namespace FishGame.Fishing
{
    public readonly struct FightEnded
    {
        public readonly string ProfileId, ItemId, RodId;
        public readonly int WeightG;
        public readonly float CastDistance, CastToBite, FightSeconds;
        public readonly FightOutcome Outcome;
        public readonly EscapeReason Reason;
        public readonly int NearSnapRecoveries;
        public readonly float MaxTension;
        public readonly bool Assist;
        public readonly bool CleanClaimed;   // owner-side claim (maxTension < threshold on a landed fish)
        public readonly bool Pity;           // R65: Machine.Roll.Pity of that cast (false without a result)

        public FightEnded(string profileId, string itemId, string rodId, int weightG, float castDistance, float castToBite, float fightSeconds,
                          FightOutcome outcome, EscapeReason reason, int nearSnapRecoveries, float maxTension, bool assist, bool cleanClaimed, bool pity)
        {
            ProfileId = profileId;
            ItemId = itemId;
            RodId = rodId;
            WeightG = weightG;
            CastDistance = castDistance;
            CastToBite = castToBite;
            FightSeconds = fightSeconds;
            Outcome = outcome;
            Reason = reason;
            NearSnapRecoveries = nearSnapRecoveries;
            MaxTension = maxTension;
            Assist = assist;
            CleanClaimed = cleanClaimed;
            Pity = pity;
        }
    }
}
