// Core/Fishing/CatchValidator.cs: pure host-side checks of casts, hooks, landings and clean-catch claims (spec 9.9, R64, C 7.9).
// Authority: Host only. Owns bait stock, rolls, ActiveCast and outcomes (spec 9.9 N6); the owner reports outcomes, the host validates and never replays the fight.
namespace FishGame.Core
{
    public static class CatchValidator
    {
        // Order: CastingAllowed, ActiveCast, StaleCastId (castId <= last), BadCharge (outside [0,1] or NaN),
        // OriginTooFar (> 3 m horizontal; a NaN origin or a non-finite yaw counts as too far), NoBait, then CastMath.ProbeReason.
        public static CastRejectReason ValidateCast(in CastRequestData req, uint lastAcceptedCastId, bool hasActive,
                                                    in HostCastView view, bool baitAvailable, float minLandDepth)
        {
            if (!view.CastingAllowed) return CastRejectReason.NotAllowedNow;
            if (hasActive) return CastRejectReason.ActiveCast;
            if (req.CastId <= lastAcceptedCastId) return CastRejectReason.StaleCastId;
            float charge = req.ChargeNorm;
            if (!(charge >= 0f && charge <= 1f)) return CastRejectReason.BadCharge;
            if (!OriginWithinSlack(req.OriginX, req.OriginZ, view.PlayerX, view.PlayerZ)) return CastRejectReason.OriginTooFar;
            if (!IsFinite(req.YawDeg)) return CastRejectReason.OriginTooFar;
            if (!baitAvailable) return CastRejectReason.NoBait;
            return CastMath.ProbeReason(in view.Probe, minLandDepth);
        }

        // Horizontal distance from the claimed cast origin to the host's view of the player is within HostOriginSlack (3 m, inclusive).
        // A NaN component fails (so does an infinite one). The adapter runs this before the physics probe (no raycast for a far origin).
        public static bool OriginWithinSlack(float originX, float originZ, float playerX, float playerZ)
        {
            float dx = originX - playerX;
            float dz = originZ - playerZ;
            float slack = FishingConstants.HostOriginSlack;
            return dx * dx + dz * dz <= slack * slack;
        }

        // now - TAccepted < TFlight + Roll.BiteWait - hookTolerance
        public static bool HookTooEarly(double now, in ActiveCast cast, float hookTolerance)
        {
            return now - cast.TAccepted < cast.TFlight + cast.Roll.BiteWait - hookTolerance;
        }

        // max(0, 0.8 * (lineLength0 - landDistance) / speed - 0.5). A non-positive speed (a data bug) gives no floor.
        public static float LandedFloorSeconds(float lineLength0, float landDistance, float speed)
        {
            if (!(speed > 0f)) return 0f;
            float s = FishingConstants.LandedFloorFactor * (lineLength0 - landDistance) / speed - FishingConstants.LandedFloorSlackSeconds;
            return s > 0f ? s : 0f;
        }

        // now - THooked < LandedFloorSeconds(LineLength0, landDistance, speed)
        public static bool LandedTooFast(double now, in ActiveCast cast, float landDistance, float speed)
        {
            return now - cast.THooked < LandedFloorSeconds(cast.LineLength0, landDistance, speed);
        }

        public static bool IsFinite(float v)
        {
            return !float.IsNaN(v) && !float.IsInfinity(v);
        }

        // The adapter checks this before it probes the physics world with a client-supplied position (T17 follow-up): all three origin
        // components and the yaw must be finite.
        public static bool IsOriginFinite(in CastRequestData req)
        {
            return IsFinite(req.OriginX) && IsFinite(req.OriginY) && IsFinite(req.OriginZ) && IsFinite(req.YawDeg);
        }

        // max(landDistance, hostDistance - HookedLineSlack); a NaN distance gives landDistance. The ledger never feeds a non-finite distance in
        // (Hooked fails closed on one), so this fallback is only a safe default for other callers.
        public static float HostLineLength0(float hostDistance, float landDistance)
        {
            float v = hostDistance - FishingConstants.HookedLineSlack;
            return v > landDistance ? v : landDistance;
        }

        // Anti-teleport bound (T9-R1): a player cannot be nearer the landing than the cast distance minus what the fastest walk
        // covers since the cast was accepted. max(hostLineLength0, castDistance - MaxWalkSpeed * elapsed - HookedLineSlack), floored at landDistance.
        // MaxWalkSpeed is the sprint speed. A NaN bound (non-finite elapsed) is ignored.
        public static float WalkBoundedLineLength0(float hostLineLength0, float castDistance, double elapsedSeconds, float landDistance)
        {
            float v = hostLineLength0;
            float bound = (float)(castDistance - MovementMath.SprintSpeed * elapsedSeconds - FishingConstants.HookedLineSlack);
            if (bound > v) v = bound;
            return v > landDistance ? v : landDistance;
        }

        // Landed -> Landed; Snapped -> Lost; Escaped after a hook with Pinned/ThrewHook/Dropped/Timeout -> Lost; everything else None.
        public static AnnounceKind AnnounceFor(FightOutcome outcome, EscapeReason reason, bool wasHooked)
        {
            switch (outcome)
            {
                case FightOutcome.Landed: return AnnounceKind.Landed;
                case FightOutcome.Snapped: return AnnounceKind.Lost;
                case FightOutcome.Escaped:
                    if (wasHooked && (reason == EscapeReason.Pinned || reason == EscapeReason.ThrewHook
                                      || reason == EscapeReason.Dropped || reason == EscapeReason.Timeout))
                        return AnnounceKind.Lost;
                    return AnnounceKind.None;
                default: return AnnounceKind.None;
            }
        }

        // The host only ever downgrades: true only when ALL bounds hold, otherwise the catch keeps its rolled quality (never Invalid).
        // The host never replays the fight (spec 9.9), so these are the physical, clock and self-consistency bounds (M2-D16).
        public static bool CleanClaimAccepted(in FightReport report, double now, in ActiveCast cast, float landDistance,
                                              float reelSpeed, float cleanMaxTension)
        {
            if (cast.Roll.Catch.IsJunk) return false;                                          // (1)
            float mt = report.MaxTension;
            if (float.IsNaN(mt) || float.IsInfinity(mt) || mt < 0f || mt >= cleanMaxTension) return false;   // (2)
            if (report.NearSnapRecoveries != 0) return false;                                  // (3) a recovery needs tension > 0.8
            float fs = report.FightSeconds;
            if (float.IsNaN(fs) || float.IsInfinity(fs)) return false;
            if (fs < LandedFloorSeconds(cast.LineLength0, landDistance, reelSpeed)) return false;           // (4)
            if (fs > (now - cast.THooked) + FishingConstants.CleanClaimClockSlackSeconds) return false;      // (5) never ahead of host
            return true;
        }
    }
}
