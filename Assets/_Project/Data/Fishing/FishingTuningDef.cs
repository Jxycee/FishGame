using System;
using FishGame.Core;
using UnityEngine;

namespace FishGame.Data
{
    // Content data only (not networked). Every spec 9.12 tuning number plus the creel caps, the clean-catch threshold and the bad-luck
    // nudge (R65). One field per FishingTuning member in struct order, except creelMaxKg (kilograms) in place of CreelMaxWeightG.
    // No tuning initialisers (C 5.1): values come from FishingSeedData via the content builder. Owned by T13.
    public sealed class FishingTuningDef : ScriptableObject
    {
        [SerializeField] private float castChargeTime;
        [SerializeField] private bool chargePingPong;
        [SerializeField] private float castMinDist;
        [SerializeField] private float minLandDepth;
        [SerializeField] private float biteWaitMin;
        [SerializeField] private float biteWaitMax;
        [SerializeField] private float minBiteWait;
        [SerializeField] private float hookBeat;
        [SerializeField] private float landDistance;
        [SerializeField] private float reelStrainBurst;
        [SerializeField] private float counterRelief;
        [SerializeField] private float steerStrain;
        [SerializeField] private float outOfWindowStrain;
        [SerializeField] private float spoolStrain;
        [SerializeField] private float spoolExtra;
        [SerializeField] private float relaxRate;
        [SerializeField] private float relaxWhileReeling;
        [SerializeField] private float reelBurstPenalty;
        [SerializeField] private float diveRunMult;
        [SerializeField] private float snapGrace;
        [SerializeField] private float snapGraceAssist;
        [SerializeField] private float strainScale;
        [SerializeField] private float strainScaleAssist;
        [SerializeField] private float pinEscapeTime;
        [SerializeField] private float slackEscapeTime;
        [SerializeField] private float fishStaminaRegen;
        [SerializeField] private float calmCenterPull;
        [SerializeField] private float lateralMaxDeg;
        [SerializeField] private float hookedLeash;
        [SerializeField] private float junkDragSpeed;
        [SerializeField] private float resultTimeout;
        [SerializeField] private float commitTimeout;
        [SerializeField] private float hookTolerance;
        [SerializeField] private float maxFightTime;
        [SerializeField] private int rodStateHz;
        [SerializeField] private bool cancelRefundsBait;
        [SerializeField] private int creelMaxEntries;
        [SerializeField] private float creelMaxKg;
        [SerializeField] private float cleanCatchMaxTension;
        [SerializeField] private int pityAfterFailures;
        [SerializeField] private float pityStrainMult;
        [SerializeField] private float pityGraceMult;
        [SerializeField] private float pityHookWindowBonus;

        public float CastChargeTime => castChargeTime;
        public bool ChargePingPong => chargePingPong;
        public float CastMinDist => castMinDist;
        public float MinLandDepth => minLandDepth;
        public float BiteWaitMin => biteWaitMin;
        public float BiteWaitMax => biteWaitMax;
        public float MinBiteWait => minBiteWait;
        public float HookBeat => hookBeat;
        public float LandDistance => landDistance;
        public float ReelStrainBurst => reelStrainBurst;
        public float CounterRelief => counterRelief;
        public float SteerStrain => steerStrain;
        public float OutOfWindowStrain => outOfWindowStrain;
        public float SpoolStrain => spoolStrain;
        public float SpoolExtra => spoolExtra;
        public float RelaxRate => relaxRate;
        public float RelaxWhileReeling => relaxWhileReeling;
        public float ReelBurstPenalty => reelBurstPenalty;
        public float DiveRunMult => diveRunMult;
        public float SnapGrace => snapGrace;
        public float SnapGraceAssist => snapGraceAssist;
        public float StrainScale => strainScale;
        public float StrainScaleAssist => strainScaleAssist;
        public float PinEscapeTime => pinEscapeTime;
        public float SlackEscapeTime => slackEscapeTime;
        public float FishStaminaRegen => fishStaminaRegen;
        public float CalmCenterPull => calmCenterPull;
        public float LateralMaxDeg => lateralMaxDeg;
        public float HookedLeash => hookedLeash;
        public float JunkDragSpeed => junkDragSpeed;
        public float ResultTimeout => resultTimeout;
        public float CommitTimeout => commitTimeout;
        public float HookTolerance => hookTolerance;
        public float MaxFightTime => maxFightTime;
        public int RodStateHz => rodStateHz;
        public bool CancelRefundsBait => cancelRefundsBait;
        public int CreelMaxEntries => creelMaxEntries;
        public float CreelMaxKg => creelMaxKg;
        public float CleanCatchMaxTension => cleanCatchMaxTension;
        public int PityAfterFailures => pityAfterFailures;
        public float PityStrainMult => pityStrainMult;
        public float PityGraceMult => pityGraceMult;
        public float PityHookWindowBonus => pityHookWindowBonus;

        // Used by the content builder and by tests. creelMaxKg = CreelMaxWeightG / 1000.
        public void Configure(in FishingTuning t)
        {
            castChargeTime = t.CastChargeTime;
            chargePingPong = t.ChargePingPong;
            castMinDist = t.CastMinDist;
            minLandDepth = t.MinLandDepth;
            biteWaitMin = t.BiteWaitMin;
            biteWaitMax = t.BiteWaitMax;
            minBiteWait = t.MinBiteWait;
            hookBeat = t.HookBeat;
            landDistance = t.LandDistance;
            reelStrainBurst = t.ReelStrainBurst;
            counterRelief = t.CounterRelief;
            steerStrain = t.SteerStrain;
            outOfWindowStrain = t.OutOfWindowStrain;
            spoolStrain = t.SpoolStrain;
            spoolExtra = t.SpoolExtra;
            relaxRate = t.RelaxRate;
            relaxWhileReeling = t.RelaxWhileReeling;
            reelBurstPenalty = t.ReelBurstPenalty;
            diveRunMult = t.DiveRunMult;
            snapGrace = t.SnapGrace;
            snapGraceAssist = t.SnapGraceAssist;
            strainScale = t.StrainScale;
            strainScaleAssist = t.StrainScaleAssist;
            pinEscapeTime = t.PinEscapeTime;
            slackEscapeTime = t.SlackEscapeTime;
            fishStaminaRegen = t.FishStaminaRegen;
            calmCenterPull = t.CalmCenterPull;
            lateralMaxDeg = t.LateralMaxDeg;
            hookedLeash = t.HookedLeash;
            junkDragSpeed = t.JunkDragSpeed;
            resultTimeout = t.ResultTimeout;
            commitTimeout = t.CommitTimeout;
            hookTolerance = t.HookTolerance;
            maxFightTime = t.MaxFightTime;
            rodStateHz = t.RodStateHz;
            cancelRefundsBait = t.CancelRefundsBait;
            creelMaxEntries = t.CreelMaxEntries;
            creelMaxKg = t.CreelMaxWeightG / 1000f;
            cleanCatchMaxTension = t.CleanCatchMaxTension;
            pityAfterFailures = t.PityAfterFailures;
            pityStrainMult = t.PityStrainMult;
            pityGraceMult = t.PityGraceMult;
            pityHookWindowBonus = t.PityHookWindowBonus;
        }

        // CreelMaxWeightG = round(creelMaxKg * 1000) (away from zero).
        public FishingTuning ToCore()
        {
            return new FishingTuning
            {
                CastChargeTime = castChargeTime,
                ChargePingPong = chargePingPong,
                CastMinDist = castMinDist,
                MinLandDepth = minLandDepth,
                BiteWaitMin = biteWaitMin,
                BiteWaitMax = biteWaitMax,
                MinBiteWait = minBiteWait,
                HookBeat = hookBeat,
                LandDistance = landDistance,
                ReelStrainBurst = reelStrainBurst,
                CounterRelief = counterRelief,
                SteerStrain = steerStrain,
                OutOfWindowStrain = outOfWindowStrain,
                SpoolStrain = spoolStrain,
                SpoolExtra = spoolExtra,
                RelaxRate = relaxRate,
                RelaxWhileReeling = relaxWhileReeling,
                ReelBurstPenalty = reelBurstPenalty,
                DiveRunMult = diveRunMult,
                SnapGrace = snapGrace,
                SnapGraceAssist = snapGraceAssist,
                StrainScale = strainScale,
                StrainScaleAssist = strainScaleAssist,
                PinEscapeTime = pinEscapeTime,
                SlackEscapeTime = slackEscapeTime,
                FishStaminaRegen = fishStaminaRegen,
                CalmCenterPull = calmCenterPull,
                LateralMaxDeg = lateralMaxDeg,
                HookedLeash = hookedLeash,
                JunkDragSpeed = junkDragSpeed,
                ResultTimeout = resultTimeout,
                CommitTimeout = commitTimeout,
                HookTolerance = hookTolerance,
                MaxFightTime = maxFightTime,
                RodStateHz = rodStateHz,
                CancelRefundsBait = cancelRefundsBait,
                CreelMaxEntries = creelMaxEntries,
                CreelMaxWeightG = (int)Math.Round(creelMaxKg * 1000.0, MidpointRounding.AwayFromZero),
                CleanCatchMaxTension = cleanCatchMaxTension,
                PityAfterFailures = pityAfterFailures,
                PityStrainMult = pityStrainMult,
                PityGraceMult = pityGraceMult,
                PityHookWindowBonus = pityHookWindowBonus,
            };
        }
    }
}
