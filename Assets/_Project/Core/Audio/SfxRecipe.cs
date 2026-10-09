namespace FishGame.Core
{
    public readonly struct SfxRecipe
    {
        public readonly string Id;
        public readonly float LengthSeconds;
        public readonly bool Loop;
        public readonly AudioCategory Category;
        public readonly float BaseVolume;
        public readonly bool Spatial;
        public readonly float MaxDistance;
        public readonly float MinInterval;

        public SfxRecipe(string id, float lengthSeconds, bool loop, AudioCategory category,
            float baseVolume, bool spatial, float maxDistance, float minInterval)
        {
            Id = id;
            LengthSeconds = lengthSeconds;
            Loop = loop;
            Category = category;
            BaseVolume = baseVolume;
            Spatial = spatial;
            MaxDistance = maxDistance;
            MinInterval = minInterval;
        }
    }
}
