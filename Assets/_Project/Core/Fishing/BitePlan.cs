// Core/Fishing/BitePlan.cs: result of BitePlanner.Plan (spec 9.4, C 7.6). Plain data.
namespace FishGame.Core
{
    public struct BitePlan
    {
        public float BiteWait;
        public byte NibbleCount;
        public float Nibble0, Nibble1, Nibble2;   // seconds after landing; unused slots stay 0
    }
}
