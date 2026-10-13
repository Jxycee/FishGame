// Core/Ecology/ClockAnchorData.cs: the replicated day-clock anchor (spec 14.2). A client computes the minute from this plus the
// network time, so only a re-anchor (pause, scrub, new day, rate change) is sent. Authority: host writes, everyone reads.
namespace FishGame.Core
{
    public struct ClockAnchorData { public double AnchorServerTime; public float AnchorMinute; public float MinutesPerSecond; public bool Running; }
}
