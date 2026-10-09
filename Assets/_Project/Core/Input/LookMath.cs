// Core, no engine references, unit-tested. Converts raw look input to degrees (spec 11.6: mouse deg/count, stick x 180 deg/s).
namespace FishGame.Core
{
    public static class LookMath
    {
        public const float StickDegPerSecond = 180f;

        // Mouse deltas are already per-frame counts, so no dt.
        public static float MouseToDegrees(float countsDelta, float sensitivityDegPerCount)
        {
            return countsDelta * sensitivityDegPerCount;
        }

        // sign(v) * v^2 * 180 * speedMult * dt. The squared response keeps small stick deflections precise.
        public static float StickToDegrees(float stickValue, float speedMult, float dt)
        {
            float magnitude = stickValue < 0f ? -stickValue : stickValue;
            return stickValue * magnitude * StickDegPerSecond * speedMult * dt;
        }
    }
}
