// Core/Fishing/CastRequestData.cs: what the owner sends with a cast request (spec 9.3, C 7.9). Plain data; the host re-derives everything.
namespace FishGame.Core
{
    public struct CastRequestData
    {
        public uint CastId;
        public float OriginX, OriginY, OriginZ, YawDeg, ChargeNorm;
        public byte BaitIndex;
        public bool Assist;
    }
}
