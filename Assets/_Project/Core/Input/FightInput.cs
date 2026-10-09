// Core, no engine references. Fight controls (spec 8.1): steer -1..1 and reel. Default = no input.
namespace FishGame.Core
{
    public readonly struct FightInput
    {
        public readonly float Steer;
        public readonly bool Reel;

        public FightInput(float steer, bool reel)
        {
            Steer = steer;
            Reel = reel;
        }
    }
}
